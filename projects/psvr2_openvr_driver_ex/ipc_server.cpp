#include "ipc_server.h"

#include "headset_haptic_manager.h"
#include "trigger_effect_manager.h"
#include "util.h"
#include "vr_settings.h"

#include <cstdio>
#include <fstream>
#include <sstream>
#include <shlobj.h>

namespace psvr2_toolkit {
  namespace ipc {

    IpcServer *IpcServer::m_pInstance = nullptr;

    IpcServer::IpcServer()
      : m_initialized(false)
      , m_running(false)
      , m_doGaze(false)
      , m_socket{}
      , m_serverAddr{}
      , m_pGazeState(nullptr)
      , m_calibrationActive(false)
      , m_gazeOffsetX(0.0f)
      , m_gazeOffsetY(0.0f)
      , m_gazeCursorEnabled(false)
      , m_cursorSensitivity(1.0f)
      , m_cursorSmoothing(0.3f)
      , m_cursorDeadzone(0.05f)
      , m_smoothedGazeX(0.0f)
      , m_smoothedGazeY(0.0f)
      , m_driftDetectionEnabled(true)
      , m_gazeVarianceAccumulator(0.0f)
      , m_gazeVarianceSampleCount(0)
      , m_lastNotifiedDrift(0.0f)
      , m_lastDriftCheckTime(std::chrono::steady_clock::now())
    {}

    IpcServer *IpcServer::Instance() {
      if (!m_pInstance) {
        m_pInstance = new IpcServer;
      }

      return m_pInstance;
    }

    bool IpcServer::Initialized() {
      return m_initialized;
    }

    void IpcServer::Initialize() {
      if (m_initialized) {
        return;
      }

      WSADATA wsaData = {};
      int result = WSAStartup(MAKEWORD(2, 2), &wsaData);
      if (result != 0) {
        Util::DriverLog("[IPC_SERVER] WSAStartup failed. Result = {}", result);
        return;
      }

      m_initialized = true;
      m_doGaze = !VRSettings::GetBool(STEAMVR_SETTINGS_DISABLE_GAZE, SETTING_DISABLE_GAZE_DEFAULT_VALUE);
      m_doOpenness = VRSettings::GetBool(STEAMVR_SETTINGS_ENABLE_EYELID_ESTIMATION, SETTING_ENABLE_EYELID_ESTIMATION_DEFAULT_VALUE);

      HeadsetHapticManager::Instance()->Initialize();

      LoadCalibrationFile();
    }

    void IpcServer::Start() {
      if (!m_initialized || m_running) {
        return;
      }

      m_socket = socket(AF_INET, SOCK_STREAM, 0);
      if (m_socket == INVALID_SOCKET) {
        Util::DriverLog("[IPC_SERVER] Creating socket failed. LastError = {}", WSAGetLastError());
        return;
      }

      m_serverAddr.sin_family = AF_INET;
      m_serverAddr.sin_addr.s_addr = htonl(INADDR_LOOPBACK);
      m_serverAddr.sin_port = htons(IPC_SERVER_PORT);

      if (bind(m_socket, reinterpret_cast<SOCKADDR * >(&m_serverAddr), sizeof(m_serverAddr)) == SOCKET_ERROR) {
        Util::DriverLog("[IPC_SERVER] Bind failed. LastError = {}", WSAGetLastError());
        closesocket(m_socket);
        return;
      }

      if (listen(m_socket, SOMAXCONN) == SOCKET_ERROR) {
        Util::DriverLog("[IPC_SERVER] Listen failed. LastError = {}", WSAGetLastError());
        closesocket(m_socket);
        return;
      }

      m_running = true;
      m_receiveThread = std::thread(&IpcServer::ReceiveLoop, this);
    }

    void IpcServer::Stop() {
      if (!m_running) {
        return;
      }

      m_running = false;
      closesocket(m_socket);
      m_receiveThread.join();
    }

    void IpcServer::UpdateGazeState(Hmd2GazeState *pGazeState, float leftEyelidOpenness, float rightEyelidOpenness) {
      std::lock_guard<std::mutex> lock(m_gazeStateMutex);
      if (!m_pGazeState) {
        m_pGazeState = new Hmd2GazeState;
      }
      if (m_pGazeState) {
        *m_pGazeState = *pGazeState;
      }
      m_leftEyelidOpenness = leftEyelidOpenness;
      m_rightEyelidOpenness = rightEyelidOpenness;
    }

    void IpcServer::LoadCalibrationFile() {
      char documentsPath[MAX_PATH];
      if (SUCCEEDED(SHGetFolderPathA(NULL, CSIDL_MYDOCUMENTS, NULL, 0, documentsPath))) {
        std::string calibrationFilePath = std::string(documentsPath) + "\\PSVR2Calibration.txt";

        std::ifstream calibrationFile(calibrationFilePath);
        if (calibrationFile.is_open()) {
          float offsetX = 0.0f, offsetY = 0.0f;
          calibrationFile >> offsetX >> offsetY;

          if (!calibrationFile.fail()) {
            m_gazeOffsetX = offsetX;
            m_gazeOffsetY = offsetY;
            Util::DriverLog("[IPC_SERVER] Loaded gaze calibration: offsetX={}, offsetY={}", offsetX, offsetY);
          } else {
            Util::DriverLog("[IPC_SERVER] Failed to parse calibration file. Using defaults (0, 0).");
            m_gazeOffsetX = 0.0f;
            m_gazeOffsetY = 0.0f;
          }
          calibrationFile.close();
        } else {
          Util::DriverLog("[IPC_SERVER] No calibration file found. Using defaults (0, 0).");
          m_gazeOffsetX = 0.0f;
          m_gazeOffsetY = 0.0f;
        }
      } else {
        Util::DriverLog("[IPC_SERVER] Failed to get Documents folder path.");
        m_gazeOffsetX = 0.0f;
        m_gazeOffsetY = 0.0f;
      }
    }

    void IpcServer::ReceiveLoop() {
      char pBuffer[1024] = {}; // This does not need to be static.
      sockaddr_in clientAddr = {};
      int clientAddrLen = sizeof(clientAddr);

      while (m_running) {
        SOCKADDR_IN clientAddr = {};
        int clientAddrLen = sizeof(clientAddr);

        SOCKET clientSocket = accept(m_socket, reinterpret_cast<SOCKADDR*>(&clientAddr), &clientAddrLen);
        if (clientSocket == INVALID_SOCKET) {
          int error = WSAGetLastError();
          if (m_running) {
              Util::DriverLog("[IPC_SERVER] Accept failed. LastError = {}", error);
          }
          else {
            Util::DriverLog("[IPC_SERVER] Server socket closed. Exiting receive loop.");
          }
          break;
        }

        std::thread clientThread(&IpcServer::HandleClient, this, clientSocket, clientAddr);
        clientThread.detach(); // client thread shouldnt block receive thread
      }
    }

    void IpcServer::HandleClient(SOCKET clientSocket, SOCKADDR_IN clientAddr) {
      char pBuffer[1024] = {};
      int clientPort = ntohs(clientAddr.sin_port);

      while (m_running) {

        int dwBufferSize = recv(clientSocket, pBuffer, sizeof(pBuffer), 0);
        if (dwBufferSize <= 0) {
          if (dwBufferSize == 0) {
            Util::DriverLog("[IPC_SERVER] Client on port {} disconnected.", clientPort);
          } else {
            Util::DriverLog("[IPC_SERVER] Receive failed for client on port {}. LastError = {}", clientPort, WSAGetLastError());
          }

          break;
        }

        if (dwBufferSize < sizeof(CommandHeader_t)) {
          Util::DriverLog("[IPC_SERVER] Received invalid command header size from client on port {}.", clientPort);
          continue;
        }

        HandleIpcCommand(clientSocket, clientAddr, pBuffer);
      }

      {
        std::lock_guard<std::mutex> lock(m_connectionsMutex);
        m_connections.erase(clientPort);
      }
      closesocket(clientSocket);
    }

    void IpcServer::HandleIpcCommand(SOCKET clientSocket, const sockaddr_in &clientAddr, char *pBuffer) {
      static TriggerEffectManager *pTriggerEffectManager = TriggerEffectManager::Instance();
      static HeadsetHapticManager *pHeadsetHapticManager = HeadsetHapticManager::Instance();

      uint16_t clientPort = ntohs(clientAddr.sin_port);

      CommandHeader_t *pHeader = reinterpret_cast<CommandHeader_t *>(pBuffer);
      void *pData = pBuffer + sizeof(CommandHeader_t);

      switch (pHeader->type) {
        case Command_ClientPing: {
          std::lock_guard<std::mutex> lock(m_connectionsMutex);
          if (pHeader->dataLen == 0 && m_connections.contains(clientPort)) {
            SendIpcCommand(clientSocket, Command_ServerPong); // TODO
          }
          break;
        }

        case Command_ClientRequestHandshake: {
          CommandDataServerHandshakeResult_t response;
          response.result = HandshakeResult_Failed;
          response.ipcVersion = k_unIpcVersion; // Communicate the IPC version the server supports.

          {
            std::lock_guard<std::mutex> lock(m_connectionsMutex);
            if (pHeader->dataLen == sizeof(CommandDataClientRequestHandshake_t) && !m_connections.contains(clientPort)) {
              CommandDataClientRequestHandshake_t *pRequest = reinterpret_cast<CommandDataClientRequestHandshake_t *>(pData);

              // We only want real running processes to handshake with us.
              if (Util::IsProcessRunning(pRequest->processId)) {
                m_connections[clientPort] = {
                  .clientAddr = clientAddr,
                  .ipcVersion = pRequest->ipcVersion,
                  .processId = pRequest->processId,
                  .socket = clientSocket
                };

                response.result = HandshakeResult_Success;
              }
            }
          }

          SendIpcCommand(clientSocket, Command_ServerHandshakeResult, &response, sizeof(response));
          break;
        }

        case Command_ClientRequestGazeData: {
          uint16_t ipcVersion = 0;
          bool isConnected = false;
          {
            std::lock_guard<std::mutex> lock(m_connectionsMutex);
            if (m_connections.contains(clientPort)) {
              isConnected = true;
              ipcVersion = m_connections[clientPort].ipcVersion;
            }
          }

          if (pHeader->dataLen == 0 && isConnected) {
            Hmd2GazeState gazeStateCopy;
            float leftOpenness, rightOpenness;
            bool hasGazeState;
            {
              std::lock_guard<std::mutex> lock(m_gazeStateMutex);
              hasGazeState = (m_pGazeState != nullptr);
              if (hasGazeState) {
                gazeStateCopy = *m_pGazeState;
                leftOpenness = m_leftEyelidOpenness;
                rightOpenness = m_rightEyelidOpenness;
              }
            }

            if (m_doGaze && hasGazeState) {
              // Handle old client IPC version requests here.
              if (ipcVersion == 1) {
                CommandDataServerGazeDataResult_t response = {
                  .leftEye = {
                    .isGazeOriginValid = gazeStateCopy.leftEye.isGazeOriginValid == Hmd2Bool::HMD2_BOOL_TRUE,
                    .gazeOriginMm = {
                      .x = gazeStateCopy.leftEye.gazeOriginMm.x,
                      .y = gazeStateCopy.leftEye.gazeOriginMm.y,
                      .z = gazeStateCopy.leftEye.gazeOriginMm.z,
                    },
                    .isGazeDirValid = gazeStateCopy.leftEye.isGazeDirValid == Hmd2Bool::HMD2_BOOL_TRUE,
                    .gazeDirNorm {
                      .x = gazeStateCopy.leftEye.gazeDirNorm.x,
                      .y = gazeStateCopy.leftEye.gazeDirNorm.y,
                      .z = gazeStateCopy.leftEye.gazeDirNorm.z,
                    },
                    .isPupilDiaValid = gazeStateCopy.leftEye.isPupilDiaValid == Hmd2Bool::HMD2_BOOL_TRUE,
                    .pupilDiaMm = gazeStateCopy.leftEye.pupilDiaMm,
                    .isBlinkValid = gazeStateCopy.leftEye.isBlinkValid == Hmd2Bool::HMD2_BOOL_TRUE,
                    .blink = gazeStateCopy.leftEye.blink == Hmd2Bool::HMD2_BOOL_TRUE,
                  },

                  .rightEye = {
                    .isGazeOriginValid = gazeStateCopy.rightEye.isGazeOriginValid == Hmd2Bool::HMD2_BOOL_TRUE,
                    .gazeOriginMm = {
                      .x = gazeStateCopy.rightEye.gazeOriginMm.x,
                      .y = gazeStateCopy.rightEye.gazeOriginMm.y,
                      .z = gazeStateCopy.rightEye.gazeOriginMm.z,
                    },
                    .isGazeDirValid = gazeStateCopy.rightEye.isGazeDirValid == Hmd2Bool::HMD2_BOOL_TRUE,
                    .gazeDirNorm {
                      .x = gazeStateCopy.rightEye.gazeDirNorm.x,
                      .y = gazeStateCopy.rightEye.gazeDirNorm.y,
                      .z = gazeStateCopy.rightEye.gazeDirNorm.z,
                    },
                    .isPupilDiaValid = gazeStateCopy.rightEye.isPupilDiaValid == Hmd2Bool::HMD2_BOOL_TRUE,
                    .pupilDiaMm = gazeStateCopy.rightEye.pupilDiaMm,
                    .isBlinkValid = gazeStateCopy.rightEye.isBlinkValid == Hmd2Bool::HMD2_BOOL_TRUE,
                    .blink = gazeStateCopy.rightEye.blink == Hmd2Bool::HMD2_BOOL_TRUE,
                  }
                };
                SendIpcCommand(clientSocket, Command_ServerGazeDataResult, &response, sizeof(response));
              } else if (ipcVersion == 2) {
                CommandDataServerGazeDataResult2_t response = {
                  .leftEye = {
                    .isGazeOriginValid = gazeStateCopy.leftEye.isGazeOriginValid == Hmd2Bool::HMD2_BOOL_TRUE,
                    .gazeOriginMm = {
                      .x = gazeStateCopy.leftEye.gazeOriginMm.x,
                      .y = gazeStateCopy.leftEye.gazeOriginMm.y,
                      .z = gazeStateCopy.leftEye.gazeOriginMm.z,
                    },
                    .isGazeDirValid = gazeStateCopy.leftEye.isGazeDirValid == Hmd2Bool::HMD2_BOOL_TRUE,
                    .gazeDirNorm {
                      .x = gazeStateCopy.leftEye.gazeDirNorm.x,
                      .y = gazeStateCopy.leftEye.gazeDirNorm.y,
                      .z = gazeStateCopy.leftEye.gazeDirNorm.z,
                    },
                    .isPupilDiaValid = gazeStateCopy.leftEye.isPupilDiaValid == Hmd2Bool::HMD2_BOOL_TRUE,
                    .pupilDiaMm = gazeStateCopy.leftEye.pupilDiaMm,
                    .isBlinkValid = gazeStateCopy.leftEye.isBlinkValid == Hmd2Bool::HMD2_BOOL_TRUE,
                    .blink = gazeStateCopy.leftEye.blink == Hmd2Bool::HMD2_BOOL_TRUE,
                    .isOpenEnabled = m_doOpenness,
                    .open = m_doOpenness ? leftOpenness : 0.0f,
                  },

                  .rightEye = {
                    .isGazeOriginValid = gazeStateCopy.rightEye.isGazeOriginValid == Hmd2Bool::HMD2_BOOL_TRUE,
                    .gazeOriginMm = {
                      .x = gazeStateCopy.rightEye.gazeOriginMm.x,
                      .y = gazeStateCopy.rightEye.gazeOriginMm.y,
                      .z = gazeStateCopy.rightEye.gazeOriginMm.z,
                    },
                    .isGazeDirValid = gazeStateCopy.rightEye.isGazeDirValid == Hmd2Bool::HMD2_BOOL_TRUE,
                    .gazeDirNorm {
                      .x = gazeStateCopy.rightEye.gazeDirNorm.x,
                      .y = gazeStateCopy.rightEye.gazeDirNorm.y,
                      .z = gazeStateCopy.rightEye.gazeDirNorm.z,
                    },
                    .isPupilDiaValid = gazeStateCopy.rightEye.isPupilDiaValid == Hmd2Bool::HMD2_BOOL_TRUE,
                    .pupilDiaMm = gazeStateCopy.rightEye.pupilDiaMm,
                    .isBlinkValid = gazeStateCopy.rightEye.isBlinkValid == Hmd2Bool::HMD2_BOOL_TRUE,
                    .blink = gazeStateCopy.rightEye.blink == Hmd2Bool::HMD2_BOOL_TRUE,
                    .isOpenEnabled = m_doOpenness,
                    .open = m_doOpenness ? rightOpenness : 0.0f,
                  }
                };
                SendIpcCommand(clientSocket, Command_ServerGazeDataResult, &response, sizeof(response));
              } else {
                CommandDataServerGazeDataResult3_t response = {
                  .leftEye = {
                    .isGazeOriginValid = gazeStateCopy.leftEye.isGazeOriginValid == Hmd2Bool::HMD2_BOOL_TRUE,
                    .gazeOriginMm = {
                      .x = gazeStateCopy.leftEye.gazeOriginMm.x,
                      .y = gazeStateCopy.leftEye.gazeOriginMm.y,
                      .z = gazeStateCopy.leftEye.gazeOriginMm.z,
                    },
                    .isGazeDirValid = gazeStateCopy.leftEye.isGazeDirValid == Hmd2Bool::HMD2_BOOL_TRUE,
                    .gazeDirNorm {
                      .x = gazeStateCopy.leftEye.gazeDirNorm.x,
                      .y = gazeStateCopy.leftEye.gazeDirNorm.y,
                      .z = gazeStateCopy.leftEye.gazeDirNorm.z,
                    },
                    .isPupilDiaValid = gazeStateCopy.leftEye.isPupilDiaValid == Hmd2Bool::HMD2_BOOL_TRUE,
                    .pupilDiaMm = gazeStateCopy.leftEye.pupilDiaMm,
                    .isPupilPosInSensorValid = gazeStateCopy.leftEye.isPupilPosInSensorValid == Hmd2Bool::HMD2_BOOL_TRUE,
                    .pupilPosInSensor = {
                      .x = gazeStateCopy.leftEye.pupilPosInSensor.x,
                      .y = gazeStateCopy.leftEye.pupilPosInSensor.y,
                    },
                    .isPosGuideValid = gazeStateCopy.leftEye.isPosGuideValid == Hmd2Bool::HMD2_BOOL_TRUE,
                    .posGuide = {
                      .x = gazeStateCopy.leftEye.posGuide.x,
                      .y = gazeStateCopy.leftEye.posGuide.y,
                    },
                    .isBlinkValid = gazeStateCopy.leftEye.isBlinkValid == Hmd2Bool::HMD2_BOOL_TRUE,
                    .blink = gazeStateCopy.leftEye.blink == Hmd2Bool::HMD2_BOOL_TRUE,
                    .isOpenEnabled = m_doOpenness,
                    .open = m_doOpenness ? leftOpenness : 0.0f,
                  },

                  .rightEye = {
                    .isGazeOriginValid = gazeStateCopy.rightEye.isGazeOriginValid == Hmd2Bool::HMD2_BOOL_TRUE,
                    .gazeOriginMm = {
                      .x = gazeStateCopy.rightEye.gazeOriginMm.x,
                      .y = gazeStateCopy.rightEye.gazeOriginMm.y,
                      .z = gazeStateCopy.rightEye.gazeOriginMm.z,
                    },
                    .isGazeDirValid = gazeStateCopy.rightEye.isGazeDirValid == Hmd2Bool::HMD2_BOOL_TRUE,
                    .gazeDirNorm {
                      .x = gazeStateCopy.rightEye.gazeDirNorm.x,
                      .y = gazeStateCopy.rightEye.gazeDirNorm.y,
                      .z = gazeStateCopy.rightEye.gazeDirNorm.z,
                    },
                    .isPupilDiaValid = gazeStateCopy.rightEye.isPupilDiaValid == Hmd2Bool::HMD2_BOOL_TRUE,
                    .pupilDiaMm = gazeStateCopy.rightEye.pupilDiaMm,
                    .isPupilPosInSensorValid = gazeStateCopy.rightEye.isPupilPosInSensorValid == Hmd2Bool::HMD2_BOOL_TRUE,
                    .pupilPosInSensor = {
                      .x = gazeStateCopy.rightEye.pupilPosInSensor.x,
                      .y = gazeStateCopy.rightEye.pupilPosInSensor.y,
                    },
                    .isPosGuideValid = gazeStateCopy.rightEye.isPosGuideValid == Hmd2Bool::HMD2_BOOL_TRUE,
                    .posGuide = {
                      .x = gazeStateCopy.rightEye.posGuide.x,
                      .y = gazeStateCopy.rightEye.posGuide.y,
                    },
                    .isBlinkValid = gazeStateCopy.rightEye.isBlinkValid == Hmd2Bool::HMD2_BOOL_TRUE,
                    .blink = gazeStateCopy.rightEye.blink == Hmd2Bool::HMD2_BOOL_TRUE,
                    .isOpenEnabled = m_doOpenness,
                    .open = m_doOpenness ? rightOpenness : 0.0f,
                  }
                };
                SendIpcCommand(clientSocket, Command_ServerGazeDataResult, &response, sizeof(response));
              }
            } else {
              // Handle old client IPC version requests here, as well.
              if (ipcVersion == 1) {
                CommandDataServerGazeDataResult_t response = {};
                SendIpcCommand(clientSocket, Command_ServerGazeDataResult, &response, sizeof(response));
              } else if (ipcVersion == 2) {
                CommandDataServerGazeDataResult2_t response = {};
                SendIpcCommand(clientSocket, Command_ServerGazeDataResult, &response, sizeof(response));
              } else {
                CommandDataServerGazeDataResult3_t response = {};
                SendIpcCommand(clientSocket, Command_ServerGazeDataResult, &response, sizeof(response));
              }
            }

          }
          break;
        }

        case Command_ClientTriggerEffectOff:
        case Command_ClientTriggerEffectFeedback:
        case Command_ClientTriggerEffectWeapon:
        case Command_ClientTriggerEffectVibration:
        case Command_ClientTriggerEffectMultiplePositionFeedback:
        case Command_ClientTriggerEffectSlopeFeedback:
        case Command_ClientTriggerEffectMultiplePositionVibration: {
          uint32_t processId = 0;
          bool isConnected = false;
          {
            std::lock_guard<std::mutex> lock(m_connectionsMutex);
            if (m_connections.contains(clientPort)) {
              isConnected = true;
              processId = m_connections[clientPort].processId;
            }
          }
          if (isConnected) {
            pTriggerEffectManager->HandleIpcCommand(processId, pHeader, pData);
          }
          break;
        }

        case Command_ClientStartGazeCalibration: {
          std::lock_guard<std::mutex> lock(m_connectionsMutex);
          if (pHeader->dataLen == 0 && m_connections.contains(clientPort)) {
            m_calibrationActive = true;
            Util::DriverLog("[IPC_SERVER] Gaze calibration started");
          }
          break;
        }

        case Command_ClientStopGazeCalibration: {
          std::lock_guard<std::mutex> lock(m_connectionsMutex);
          if (pHeader->dataLen == 0 && m_connections.contains(clientPort)) {
            m_calibrationActive = false;
            LoadCalibrationFile();
            Util::DriverLog("[IPC_SERVER] Gaze calibration stopped, calibration file reloaded");
          }
          break;
        }

        case Command_ClientEnableGazeCursor: {
          std::lock_guard<std::mutex> lock(m_connectionsMutex);
          if (pHeader->dataLen == sizeof(CommandDataClientGazeCursorControl_t) && m_connections.contains(clientPort)) {
            CommandDataClientGazeCursorControl_t *pRequest = reinterpret_cast<CommandDataClientGazeCursorControl_t *>(pData);
            m_gazeCursorEnabled = true;
            m_cursorSensitivity = pRequest->sensitivity;
            m_cursorSmoothing = pRequest->smoothing;
            m_cursorDeadzone = pRequest->deadzone;
            m_smoothedGazeX = 0.0f;
            m_smoothedGazeY = 0.0f;
            Util::DriverLog("[IPC_SERVER] Gaze cursor enabled (sensitivity: {}, smoothing: {}, deadzone: {})",
                           m_cursorSensitivity, m_cursorSmoothing, m_cursorDeadzone);
          }
          break;
        }

        case Command_ClientDisableGazeCursor: {
          std::lock_guard<std::mutex> lock(m_connectionsMutex);
          if (pHeader->dataLen == 0 && m_connections.contains(clientPort)) {
            m_gazeCursorEnabled = false;
            Util::DriverLog("[IPC_SERVER] Gaze cursor disabled");
          }
          break;
        }

        case Command_ClientHeadsetHapticVibration: {
          uint32_t processId = 0;
          bool isConnected = false;
          {
            std::lock_guard<std::mutex> lock(m_connectionsMutex);
            if (m_connections.contains(clientPort)) {
              isConnected = true;
              processId = m_connections[clientPort].processId;
            }
          }
          if (isConnected) {
            pHeadsetHapticManager->HandleIpcCommand(processId, pHeader, pData);
          }
          break;
        }

      }
    }

    void IpcServer::UpdateGazeCursor(float gazeX, float gazeY) {
      if (!m_gazeCursorEnabled) {
        return;
      }

      // Apply deadzone - ignore small movements near center
      float magnitude = sqrtf(gazeX * gazeX + gazeY * gazeY);
      if (magnitude < m_cursorDeadzone) {
        return;
      }

      // Apply smoothing using exponential moving average
      m_smoothedGazeX = m_smoothedGazeX * m_cursorSmoothing + gazeX * (1.0f - m_cursorSmoothing);
      m_smoothedGazeY = m_smoothedGazeY * m_cursorSmoothing + gazeY * (1.0f - m_cursorSmoothing);

      // Get current screen dimensions
      int screenWidth = GetSystemMetrics(SM_CXSCREEN);
      int screenHeight = GetSystemMetrics(SM_CYSCREEN);

      // Get current cursor position
      POINT cursorPos;
      if (!GetCursorPos(&cursorPos)) {
        return;
      }

      // Convert gaze direction to screen delta
      // Gaze X/Y are normalized direction vectors, scale by sensitivity
      float deltaX = m_smoothedGazeX * m_cursorSensitivity * 500.0f;  // Scale factor for reasonable movement
      float deltaY = -m_smoothedGazeY * m_cursorSensitivity * 500.0f; // Invert Y for screen coordinates

      // Calculate new cursor position
      int newX = cursorPos.x + static_cast<int>(deltaX);
      int newY = cursorPos.y + static_cast<int>(deltaY);

      // Clamp to screen bounds
      newX = max(0, min(newX, screenWidth - 1));
      newY = max(0, min(newY, screenHeight - 1));

      // Move cursor
      SetCursorPos(newX, newY);
    }

    void IpcServer::CheckGazeDrift(float gazeX, float gazeY) {
      if (!m_driftDetectionEnabled || m_calibrationActive) {
        return;
      }

      // Calculate variance from center (drift indicator)
      float variance = gazeX * gazeX + gazeY * gazeY;
      m_gazeVarianceAccumulator += variance;
      m_gazeVarianceSampleCount++;

      // Check drift every 5 seconds
      auto now = std::chrono::steady_clock::now();
      auto elapsed = std::chrono::duration_cast<std::chrono::seconds>(now - m_lastDriftCheckTime).count();

      if (elapsed >= 5 && m_gazeVarianceSampleCount > 0) {
        float avgVariance = m_gazeVarianceAccumulator / m_gazeVarianceSampleCount;

        // Drift threshold: if average variance exceeds 0.15, suggest recalibration
        // Only notify if drift increased significantly since last notification
        const float DRIFT_THRESHOLD = 0.15f;
        const float NOTIFICATION_COOLDOWN = 300.0f; // 5 minutes between notifications

        if (avgVariance > DRIFT_THRESHOLD && (avgVariance - m_lastNotifiedDrift) > 0.05f) {
          // Check if enough time passed since last notification
          static auto lastNotificationTime = std::chrono::steady_clock::now();
          auto timeSinceLastNotification = std::chrono::duration_cast<std::chrono::seconds>(now - lastNotificationTime).count();

          if (timeSinceLastNotification >= NOTIFICATION_COOLDOWN) {
            // Send notification to all connected clients via their existing connections
            {
              std::lock_guard<std::mutex> lock(m_connectionsMutex);
              for (const auto& [port, conn] : m_connections) {
                SendIpcCommand(conn.socket, Command_ServerRecalibrationNeeded);
              }
            }

            m_lastNotifiedDrift = avgVariance;
            lastNotificationTime = now;
            Util::DriverLog("[IPC_SERVER] Gaze drift detected (variance: {:.3f}), recalibration notification sent", avgVariance);
          }
        }

        // Reset accumulators
        m_gazeVarianceAccumulator = 0.0f;
        m_gazeVarianceSampleCount = 0;
        m_lastDriftCheckTime = now;
      }
    }

    void IpcServer::SendIpcCommand(SOCKET clientSocket, ECommandType type, void *pData, int dataLen) {
      char pBuffer[1024] = {};

      int actualDataLen = pData ? dataLen : 0;
      int actualBufferLen = sizeof(CommandHeader_t) + actualDataLen;

      CommandHeader_t *pHeader = reinterpret_cast<CommandHeader_t *>(pBuffer);
      pHeader->type = type;
      pHeader->dataLen = actualDataLen;
      if (pData && actualDataLen > 0) {
        memcpy(pBuffer + sizeof(CommandHeader_t), pData, actualDataLen);
      }

      send(clientSocket, pBuffer, actualBufferLen, 0);
    }

  } // ipc
} // psvr2_toolkit

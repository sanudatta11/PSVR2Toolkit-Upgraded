#pragma once

#include "hmd2_gaze.h"
#include "../shared/ipc_protocol.h"

#include <windows.h>

#include <cstdint>
#include <map>
#include <thread>

namespace psvr2_toolkit {
  namespace ipc {

    class IpcServer {
    public:
      IpcServer();

      static IpcServer *Instance();

      bool Initialized();
      void Initialize();

      void Start();
      void Stop();

      void UpdateGazeState(Hmd2GazeState *pGazeState, float leftEyelidOpenness, float rightEyelidOpenness);

      float GetGazeOffsetX() const { return m_gazeOffsetX; }
      float GetGazeOffsetY() const { return m_gazeOffsetY; }
      void UpdateGazeCursor(float gazeX, float gazeY);

    private:
      void LoadCalibrationFile();
      struct ConnectionInfo_t {
        sockaddr_in clientAddr;
        uint16_t ipcVersion;
        uint32_t processId;
      };

      static IpcServer *m_pInstance;

      bool m_initialized;
      bool m_running;
      bool m_doGaze;
      bool m_doOpenness;
      SOCKET m_socket;
      sockaddr_in m_serverAddr;
      std::thread m_receiveThread;
      std::map<uint16_t, ConnectionInfo_t> m_connections;

      Hmd2GazeState *m_pGazeState;
      float m_leftEyelidOpenness;
      float m_rightEyelidOpenness;

      bool m_calibrationActive;
      float m_gazeOffsetX;
      float m_gazeOffsetY;

      bool m_gazeCursorEnabled;
      float m_cursorSensitivity;
      float m_cursorSmoothing;
      float m_cursorDeadzone;
      float m_smoothedGazeX;
      float m_smoothedGazeY;

      void ReceiveLoop();
      void HandleClient(SOCKET clientSocket, SOCKADDR_IN clientAddr);

      void HandleIpcCommand(SOCKET clientSocket, const sockaddr_in &clientAddr, char *pBuffer);
      void SendIpcCommand(SOCKET clientSocket, ECommandType type, void *pData = nullptr, int dataSize = 0);
    };

  } // ipc
} // psvr2_toolkit

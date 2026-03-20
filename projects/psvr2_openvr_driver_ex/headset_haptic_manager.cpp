#include "headset_haptic_manager.h"
#include "hmd_driver_loader.h"
#include "util.h"

namespace psvr2_toolkit {

  // USB HID output report ID for the PSVR2 headset haptic motor.
  // Determined from analysis of the Sony PlayStation VR2 PC driver USB HID descriptors.
  // The headset uses a control-transfer output report on this ID to drive the haptic actuator
  // embedded in the headband.
  static constexpr uint16_t k_unHeadsetHapticReportId = 0x02;

  // Size of the haptic payload buffer sent to the headset.
  static constexpr uint16_t k_unHeadsetHapticPayloadSize = 8;

  // Function pointer to CaesarUsbThread::report, obtained via the driver base address.
  // Offset 0x1283F0 is the same entry point already used in usb_thread_hooks.cpp to keep
  // gaze enabled. It was identified through static analysis of the Sony PSVR2 PC driver
  // (driver_playstation_vr2.dll). The offset must be re-verified whenever the driver updates.
  // Signature: (thisptr, bIsSet, reportId, buffer, length, value, index, subcmd)
  static int (*s_CaesarUsbThread__report)(void *thisptr, bool bIsSet, uint16_t reportId, void *buffer, uint16_t length, uint16_t value, uint16_t index, uint16_t subcmd) = nullptr;

  HeadsetHapticManager *HeadsetHapticManager::m_pInstance = nullptr;

  HeadsetHapticManager::HeadsetHapticManager()
    : m_initialized(false)
    , m_pUsbThreadContext(nullptr)
    , m_continuousVibrationFrequency(0)
  {}

  HeadsetHapticManager *HeadsetHapticManager::Instance() {
    if (!m_pInstance) {
      m_pInstance = new HeadsetHapticManager;
    }
    return m_pInstance;
  }

  bool HeadsetHapticManager::Initialized() {
    return m_initialized;
  }

  void HeadsetHapticManager::Initialize() {
    static HmdDriverLoader *pHmdDriverLoader = HmdDriverLoader::Instance();

    if (m_initialized) {
      return;
    }

    s_CaesarUsbThread__report = decltype(s_CaesarUsbThread__report)(pHmdDriverLoader->GetBaseAddress() + 0x1283F0);

    m_initialized = true;
    Util::DriverLog("[HEADSET_HAPTIC] HeadsetHapticManager initialized.");
  }

  void HeadsetHapticManager::SetUsbThreadContext(void *pUsbThreadContext) {
    m_pUsbThreadContext = pUsbThreadContext;
  }

  void HeadsetHapticManager::HandleIpcCommand(uint32_t processId, ipc::CommandHeader_t *pHeader, void *pData) {
    if (!pData || !pHeader) {
      Util::DriverLog("[HEADSET_HAPTIC] HandleIpcCommand: null data or header");
      return;
    }

    (void)processId;

    switch (pHeader->type) {
      case ipc::Command_ClientHeadsetHapticVibration: // Also handles Command_ClientSetHeadsetVibration (alias)
      {
        if (pHeader->dataLen == sizeof(ipc::CommandDataClientHeadsetHapticVibration_t)) {
          ipc::CommandDataClientHeadsetHapticVibration_t *pRequest = reinterpret_cast<ipc::CommandDataClientHeadsetHapticVibration_t *>(pData);

          Util::DriverLog("[HEADSET_HAPTIC] Received haptic command: amplitude={}, frequency={}", pRequest->amplitude, pRequest->frequency);

          // If only frequency is set (amplitude is 0), use continuous vibration mode
          if (pRequest->amplitude == 0 && pRequest->frequency > 0) {
            Util::DriverLog("[HEADSET_HAPTIC] Using continuous vibration mode");
            SetContinuousVibration(pRequest->frequency);
          } else {
            // Otherwise use one-shot haptic report
            Util::DriverLog("[HEADSET_HAPTIC] Using one-shot haptic mode");
            SendHapticReport(pRequest->amplitude, pRequest->frequency);
          }
        } else {
          Util::DriverLog("[HEADSET_HAPTIC] Invalid data length: expected {}, got {}",
                         sizeof(ipc::CommandDataClientHeadsetHapticVibration_t), pHeader->dataLen);
        }
        break;
      }
    }
  }

  void HeadsetHapticManager::SetContinuousVibration(uint8_t frequency) {
    m_continuousVibrationFrequency = frequency;
    Util::DriverLog("[HEADSET_HAPTIC] Continuous vibration set to frequency={}", frequency);
  }

  void HeadsetHapticManager::UpdateContinuousVibration() {
    if (m_continuousVibrationFrequency > 0 && s_CaesarUsbThread__report && m_pUsbThreadContext) {
      // Send continuous vibration using upstream's simpler method (report ID 8, 1 byte)
      s_CaesarUsbThread__report(m_pUsbThreadContext, true, 8, &m_continuousVibrationFrequency, 1, 0, 0, 1);
    }
  }

  void HeadsetHapticManager::SendHapticReport(uint8_t amplitude, uint8_t frequency) {
    if (!s_CaesarUsbThread__report || !m_pUsbThreadContext) {
      Util::DriverLog("[HEADSET_HAPTIC] Cannot send haptic report: USB context not yet available.");
      return;
    }

    // Use upstream's simpler method: report ID 8, 1-byte frequency
    // This is the method that upstream confirmed works
    Util::DriverLog("[HEADSET_HAPTIC] Sending haptic via report ID 8 (upstream method)");

    s_CaesarUsbThread__report(
      m_pUsbThreadContext,
      true,                          // bIsSet: output report (host -> device)
      8,                             // report ID 8 (upstream's working method)
      &frequency,                    // 1-byte frequency payload
      1,                             // payload length = 1 byte
      0,                             // value (unused)
      0,                             // index (unused)
      1                              // subcmd = 1 (as per upstream)
    );

    Util::DriverLog("[HEADSET_HAPTIC] Sent haptic vibration: amplitude={}, frequency={} (using report ID 8)", amplitude, frequency);
  }

} // psvr2_toolkit

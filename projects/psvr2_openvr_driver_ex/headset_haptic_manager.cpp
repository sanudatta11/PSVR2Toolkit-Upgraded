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
      return;
    }

    (void)processId;

    switch (pHeader->type) {
      case ipc::Command_ClientHeadsetHapticVibration: {
        if (pHeader->dataLen == sizeof(ipc::CommandDataClientHeadsetHapticVibration_t)) {
          ipc::CommandDataClientHeadsetHapticVibration_t *pRequest = reinterpret_cast<ipc::CommandDataClientHeadsetHapticVibration_t *>(pData);
          SendHapticReport(pRequest->amplitude, pRequest->frequency);
        }
        break;
      }
    }
  }

  void HeadsetHapticManager::SendHapticReport(uint8_t amplitude, uint8_t frequency) {
    if (!s_CaesarUsbThread__report || !m_pUsbThreadContext) {
      Util::DriverLog("[HEADSET_HAPTIC] Cannot send haptic report: USB context not yet available.");
      return;
    }

    // Build the haptic payload for the PSVR2 headset haptic motor.
    // Byte 0: amplitude (motor strength, 0-255)
    // Byte 1: frequency (vibration rate in Hz, 0-255)
    // Bytes 2-7: reserved / padding
    uint8_t payload[k_unHeadsetHapticPayloadSize] = {};
    payload[0] = amplitude;
    payload[1] = frequency;

    s_CaesarUsbThread__report(
      m_pUsbThreadContext,
      true,                          // bIsSet: output report (host -> device)
      k_unHeadsetHapticReportId,     // report ID for headset haptic motor
      payload,                       // haptic payload
      k_unHeadsetHapticPayloadSize,  // payload length
      0,                             // value (unused for HID output)
      0,                             // index (unused for HID output)
      0                              // subcmd (unused)
    );

    Util::DriverLog("[HEADSET_HAPTIC] Sent haptic vibration: amplitude={}, frequency={}", amplitude, frequency);
  }

} // psvr2_toolkit

#pragma once

#include "../shared/ipc_protocol.h"

#include <cstdint>

namespace psvr2_toolkit {

  class HeadsetHapticManager {
  public:
    HeadsetHapticManager();

    static HeadsetHapticManager *Instance();

    bool Initialized();
    void Initialize();

    // Called by UsbThreadHooks to supply the USB thread context pointer required
    // to send output reports to the headset.
    void SetUsbThreadContext(void *pUsbThreadContext);

    void HandleIpcCommand(uint32_t processId, ipc::CommandHeader_t *pHeader, void *pData);

    // Continuous vibration (upstream-style, called from USB polling loop)
    void UpdateContinuousVibration();
    void SetContinuousVibration(uint8_t frequency);
    uint8_t GetContinuousVibration() const { return m_continuousVibrationFrequency; }

  private:
    static HeadsetHapticManager *m_pInstance;

    bool m_initialized;
    void *m_pUsbThreadContext;
    uint8_t m_continuousVibrationFrequency; // For upstream-style continuous vibration

    void SendHapticReport(uint8_t amplitude, uint8_t frequency);
  };

} // psvr2_toolkit

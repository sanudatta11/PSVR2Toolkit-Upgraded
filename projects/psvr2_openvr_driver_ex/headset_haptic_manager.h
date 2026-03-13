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

  private:
    static HeadsetHapticManager *m_pInstance;

    bool m_initialized;
    void *m_pUsbThreadContext;

    void SendHapticReport(uint8_t amplitude, uint8_t frequency);
  };

} // psvr2_toolkit

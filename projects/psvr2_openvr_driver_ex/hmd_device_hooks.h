#pragma once

#include <openvr_driver.h>

namespace psvr2_toolkit {

  class HmdDeviceHooks {
  public:
    static void InstallHooks();
    static void UpdateGaze(void* pData, size_t dwSize);
    static void EnableCalibration(bool enable);
    static void ReloadCalibration();
  };

} // psvr2_toolkit

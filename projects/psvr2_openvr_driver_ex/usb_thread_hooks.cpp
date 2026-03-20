#include "usb_thread_hooks.h"

#include "headset_haptic_manager.h"
#include "hmd_driver_loader.h"
#include "hook_lib.h"
#include "vr_settings.h"
#include "util.h"

namespace psvr2_toolkit {

  int (*CaesarUsbThread__report)(void *thisptr, bool bIsSet, uint16_t reportId, void *buffer, uint16_t length, uint16_t value, uint16_t index, uint16_t subcmd);

  static bool s_gazeEnabled = true;

  int (*CaesarUsbThreadImuStatus__poll)(void *) = nullptr;
  int CaesarUsbThreadImuStatus__pollHook(void *thisptr) {
    int result = CaesarUsbThreadImuStatus__poll(thisptr);

    static bool s_firstPoll = true;
    if (s_firstPoll) {
      Util::DriverLog("[USB_THREAD] USB polling loop started - haptics now available");
      s_firstPoll = false;
    }

    if (s_gazeEnabled) {
      CaesarUsbThread__report(thisptr, true, 12, nullptr, 0, 0, 0, 1); // Keep gaze enabled
    }

    // Always provide the USB thread context to HeadsetHapticManager so it can send
    // headset haptic output reports regardless of whether gaze tracking is active.
    HeadsetHapticManager *pHapticManager = HeadsetHapticManager::Instance();
    pHapticManager->SetUsbThreadContext(thisptr);

    // Update continuous vibration if enabled (upstream-style)
    pHapticManager->UpdateContinuousVibration();

    return result;
  }

  void UsbThreadHooks::InstallHooks() {
    static HmdDriverLoader *pHmdDriverLoader = HmdDriverLoader::Instance();

    CaesarUsbThread__report = decltype(CaesarUsbThread__report)(pHmdDriverLoader->GetBaseAddress() + 0x1283F0);

    s_gazeEnabled = !VRSettings::GetBool(STEAMVR_SETTINGS_DISABLE_GAZE, SETTING_DISABLE_GAZE_DEFAULT_VALUE);

    // Always hook CaesarUsbThreadImuStatus::poll to supply the USB thread context to
    // HeadsetHapticManager. Inside the hook, gaze-specific work is conditional on
    // s_gazeEnabled so headset haptics function even when gaze tracking is disabled.
    HookLib::InstallHook(reinterpret_cast<void *>(pHmdDriverLoader->GetBaseAddress() + 0x1268D0),
                         reinterpret_cast<void *>(CaesarUsbThreadImuStatus__pollHook),
                         reinterpret_cast<void **>(&CaesarUsbThreadImuStatus__poll));
  }

} // psvr2_toolkit

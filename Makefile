POWERSHELL ?= powershell

.PHONY: help build-dll clean-dll

help:
	@echo "Targets:"
	@echo "  make build-dll       Build only driver_playstation_vr2.dll (Debug|x64)"
	@echo "  make clean-dll       Clean only the driver project (Debug|x64)"

build-dll:
	$(POWERSHELL) -NoProfile -ExecutionPolicy Bypass -Command "$$msb = .\scripts\get-msbuild-path.ps1; & $$msb .\projects\psvr2_openvr_driver_ex\psvr2_openvr_driver_ex.vcxproj /p:Configuration=Debug /p:Platform=x64 /v:m"

clean-dll:
	$(POWERSHELL) -NoProfile -ExecutionPolicy Bypass -Command "$$msb = .\scripts\get-msbuild-path.ps1; & $$msb .\projects\psvr2_openvr_driver_ex\psvr2_openvr_driver_ex.vcxproj /t:Clean /p:Configuration=Debug /p:Platform=x64 /v:m"

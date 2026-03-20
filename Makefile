POWERSHELL ?= powershell
CONFIGURATION ?= Release
MSBUILD = msbuild

.PHONY: help all build build-driver build-app update-driver run run-app run-steamvr restart-bluetooth clean clean-driver clean-app

help:
	@echo "PSVR2 Toolkit Makefile"
	@echo ""
	@echo "Targets:"
	@echo "  make all             Build everything and update driver"
	@echo "  make build           Build driver and companion app"
	@echo "  make build-driver    Build only the driver DLL"
	@echo "  make build-app       Build only the companion app"
	@echo "  make update-driver   Build and install driver (requires admin)"
	@echo "  make run             Run SteamVR and companion app"
	@echo "  make run-app         Run the companion app only"
	@echo "  make run-steamvr     Run SteamVR only"
	@echo "  make restart-bluetooth  Restart Bluetooth service (requires admin)"
	@echo "  make clean           Clean all build outputs"
	@echo "  make clean-driver    Clean driver build outputs"
	@echo "  make clean-app       Clean app build outputs"
	@echo ""
	@echo "Variables:"
	@echo "  CONFIGURATION=Release|Debug (default: Release)"

all: build update-driver
	@echo "Build and driver update complete!"

build: build-driver build-app
	@echo "All projects built successfully!"

build-driver:
	@echo "Building driver ($(CONFIGURATION))..."
	nuget restore PSVR2Toolkit.sln
	$(MSBUILD) /m /p:Configuration=$(CONFIGURATION) /p:Platform=x64 /t:psvr2_openvr_driver_ex:Rebuild PSVR2Toolkit.sln

build-app:
	@echo "Building IPC library ($(CONFIGURATION))..."
	$(MSBUILD) /m /p:Configuration=$(CONFIGURATION) /t:Rebuild projects\PSVR2Toolkit.IPC\PSVR2Toolkit.IPC.csproj
	@echo "Building companion app ($(CONFIGURATION))..."
	$(MSBUILD) /m /p:Configuration=$(CONFIGURATION) /t:Rebuild projects\PSVR2Toolkit.App\PSVR2Toolkit.App.csproj

update-driver: build-driver
	@echo "Installing driver (requires Administrator)..."
	@$(POWERSHELL) -NoProfile -Command "Start-Process powershell -Verb RunAs -ArgumentList '-NoProfile -ExecutionPolicy Bypass -Command \"Set-Location ''$(shell cd)''; & ''.\update-driver.ps1'' -Configuration $(CONFIGURATION) -SkipBuild\"' -Wait"

run: run-steamvr run-app
	@echo "SteamVR and companion app launched!"

run-steamvr:
	@echo "Starting SteamVR..."
	@$(POWERSHELL) -NoProfile -Command "Start-Process 'steam://rungameid/250820'"

run-app:
	@echo "Starting PSVR2 Toolkit companion app..."
	@if exist "projects\PSVR2Toolkit.App\bin\$(CONFIGURATION)\net8.0-windows\PSVR2Toolkit.App.exe" ( \
		start "" "projects\PSVR2Toolkit.App\bin\$(CONFIGURATION)\net8.0-windows\PSVR2Toolkit.App.exe" \
	) else ( \
		echo ERROR: App not built. Run 'make build-app' first. \
	)

restart-bluetooth:
	@echo "Restarting Bluetooth service (requires Administrator)..."
	@$(POWERSHELL) -NoProfile -Command "Start-Process powershell -Verb RunAs -ArgumentList '-NoProfile -Command \"Restart-Service bthserv -Force; Write-Host ''Bluetooth service restarted successfully!''; Start-Sleep -Seconds 2\"'"

clean: clean-driver clean-app
	@echo "All build outputs cleaned!"

clean-driver:
	@echo "Cleaning driver build outputs..."
	$(MSBUILD) /m /p:Configuration=$(CONFIGURATION) /p:Platform=x64 /t:psvr2_openvr_driver_ex:Clean PSVR2Toolkit.sln

clean-app:
	@echo "Cleaning app build outputs..."
	$(MSBUILD) /m /p:Configuration=$(CONFIGURATION) /t:Clean projects\PSVR2Toolkit.App\PSVR2Toolkit.App.csproj

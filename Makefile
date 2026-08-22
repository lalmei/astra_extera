SHELL := /bin/zsh

DOTNET_ROOT := $(CURDIR)/.dotnet
DOTNET_CLI_HOME := $(CURDIR)/.dotnet-home
DOTNET_ENV := PATH="$(DOTNET_ROOT):$$PATH" DOTNET_CLI_HOME="$(DOTNET_CLI_HOME)"

CONFIGURATION ?= Release
TARGET_FRAMEWORK := net10.0
GAME_APP ?= /Applications/Vintage Story.app
MODS_DIR ?= $(HOME)/Library/Application Support/VintagestoryData/Mods
DEPLOY_DIR := $(MODS_DIR)/AstraExtera
BUILD_OUTPUT_DIR := src/AstraExtera/bin/$(CONFIGURATION)/$(TARGET_FRAMEWORK)
DIST_DIR := dist
MOD_VERSION = $(shell perl -0ne 'print $$1 if /"version":\s*"([0-9]+\.[0-9]+\.[0-9]+)"/' modinfo.json)
PACKAGE_FILE = $(DIST_DIR)/AstraExtera-$(MOD_VERSION).zip

.PHONY: help test build package deploy run deploy-run

help:
	@printf "Targets:\n"
	@printf "  make test        Run the test suite\n"
	@printf "  make build       Build the mod in $(CONFIGURATION)\n"
	@printf "  make package     Build and zip the mod into $(DIST_DIR)/\n"
	@printf "  make deploy      Package the mod and install the zip into Vintage Story Mods\n"
	@printf "  make run         Launch Vintage Story.app\n"
	@printf "  make deploy-run  Deploy the mod, then launch the game\n"

test:
	@env $(DOTNET_ENV) dotnet test tests/AstraExtera.Tests/AstraExtera.Tests.csproj -c $(CONFIGURATION) -v minimal

build:
	@env $(DOTNET_ENV) dotnet build src/AstraExtera/AstraExtera.csproj -c $(CONFIGURATION) -v minimal

package: build
	@mkdir -p "$(DIST_DIR)"
	@rm -f "$(PACKAGE_FILE)"
	@cd "$(BUILD_OUTPUT_DIR)" && zip -qr "$(CURDIR)/$(PACKAGE_FILE)" .
	@printf "Packaged $(PACKAGE_FILE)\n"

deploy: package
	@mkdir -p "$(MODS_DIR)"
	@rm -rf "$(DEPLOY_DIR)"
	@rm -f "$(MODS_DIR)"/AstraExtera-*.zip(N)
	@cp "$(PACKAGE_FILE)" "$(MODS_DIR)/"
	@printf "Deployed $(PACKAGE_FILE) to $(MODS_DIR)/\n"

run:
	@open -a "$(GAME_APP)"

deploy-run: deploy run

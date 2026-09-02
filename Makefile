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

GALAXY_PREVIEW := $(DIST_DIR)/galaxy-preview.html
STAR_CATALOG := $(DIST_DIR)/star-catalog.v1.json

.PHONY: help test build package deploy run deploy-run galaxy-preview star-catalog celestial-textures moddb-preview moddb-copy bump-version bump-minor-version bump-patch-version bump-version-files

help:
	@printf "Targets:\n"
	@printf "  make test        Run the test suite\n"
	@printf "  make build       Build the mod in $(CONFIGURATION)\n"
	@printf "  make package     Build and zip the mod into $(DIST_DIR)/\n"
	@printf "  make deploy      Package the mod and install the zip into Vintage Story Mods\n"
	@printf "  make run         Launch Vintage Story.app\n"
	@printf "  make deploy-run  Deploy the mod, then launch the game\n"
	@printf "  make galaxy-preview          Write a random-seed HTML preview to $(GALAXY_PREVIEW) and open it\n"
	@printf "  make galaxy-preview SEED=42  Same, pinned to seed 42\n"
	@printf "  make star-catalog            Write $(STAR_CATALOG) without opening the preview\n"
	@printf "  make celestial-textures      Rebuild the shipped planet, moon and ring textures from the source art\n"
	@printf "                               Giants from gas_giant*_NNN.png, moons from image.png, rings from ring_assets.zip\n"
	@printf "  make star-catalog SEED=42    Same, pinned to seed 42\n"
	@printf "  make moddb-preview           Render the ModDB description locally and open it\n"
	@printf "  make moddb-copy              Copy the paste-ready ModDB description to the clipboard\n"
	@printf "  make bump-version VERSION=0.1.2  Update, build, and deploy mod version\n"
	@printf "  make bump-minor-version  Increment minor version, reset patch to 0, build, and deploy\n"
	@printf "  make bump-patch-version  Increment patch version, build, and deploy\n"

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

galaxy-preview:
	@mkdir -p "$(DIST_DIR)"
	@env $(DOTNET_ENV) dotnet run --project tools/GalaxyPreview/GalaxyPreview.csproj -c $(CONFIGURATION) -- --out "$(GALAXY_PREVIEW)" --open $(if $(SEED),--seed $(SEED),)

# The artwork is prepared once and committed; the game never runs this. Needs Pillow and numpy.
celestial-textures:
	@python3 tools/celestial-textures/prepare.py

star-catalog:
	@mkdir -p "$(DIST_DIR)"
	@env $(DOTNET_ENV) dotnet run --project tools/GalaxyPreview/GalaxyPreview.csproj -c $(CONFIGURATION) -- --out "$(GALAXY_PREVIEW)" $(if $(SEED),--seed $(SEED),)

MODDB_SOURCE := docs/moddb-description.html
MODDB_PREVIEW := $(DIST_DIR)/moddb-preview.html

$(MODDB_PREVIEW): $(MODDB_SOURCE) tools/moddb_preview.py
	@python3 tools/moddb_preview.py --out "$(MODDB_PREVIEW)" >/dev/null

moddb-preview: $(MODDB_PREVIEW)
	@open "$(MODDB_PREVIEW)"

moddb-copy:
	@python3 tools/moddb_preview.py --paste | pbcopy
	@printf "Paste-ready ModDB description copied to the clipboard\n"

# Re-invoke make after rewriting the version so PACKAGE_FILE picks up the new number.
bump-version: bump-version-files
	@$(MAKE) deploy

bump-version-files:
	@if [[ -z "$(VERSION)" ]]; then printf "Usage: make bump-version VERSION=0.1.2\n"; exit 2; fi
	@if ! [[ "$(VERSION)" =~ ^[0-9]+\.[0-9]+\.[0-9]+$$ ]]; then printf "VERSION must look like 0.1.2\n"; exit 2; fi
	@perl -0pi -e 's/"version":\s*"[^"]+"/"version": "$(VERSION)"/' modinfo.json
	@perl -0pi -e 's/public const string Version = "[^"]+";/public const string Version = "$(VERSION)";/' src/AstraExtera/AstraExteraModMetadata.cs
	@printf "Bumped AstraExtera source version to $(VERSION)\n"

bump-minor-version:
	@current=$$(perl -0ne 'print $$1 if /"version":\s*"([0-9]+\.[0-9]+\.[0-9]+)"/' modinfo.json); \
	if [[ -z "$$current" ]]; then printf "Could not read version from modinfo.json\n"; exit 2; fi; \
	parts=("$${(@s:.:)current}"); \
	new_version="$$parts[1].$$(( $$parts[2] + 1 )).0"; \
	$(MAKE) bump-version VERSION=$$new_version

bump-patch-version:
	@current=$$(perl -0ne 'print $$1 if /"version":\s*"([0-9]+\.[0-9]+\.[0-9]+)"/' modinfo.json); \
	if [[ -z "$$current" ]]; then printf "Could not read version from modinfo.json\n"; exit 2; fi; \
	parts=("$${(@s:.:)current}"); \
	new_version="$$parts[1].$$parts[2].$$(( $$parts[3] + 1 ))"; \
	$(MAKE) bump-version VERSION=$$new_version

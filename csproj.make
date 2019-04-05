#!/usr/bin/env make

.PHONY: csproj
csproj: csproj.in
	sed 's/DOTNET_FRAMEWORK/$(DOTNET_FRAMEWORK)/g; s/PLUGIN_VERSION/$(VERSION)/g; s/JELLYFIN_VERSION/$(JELLYFIN_VERSION)/g' $< > $(CSPROJ) || rm -f $(CSPROJ)

clean:
	rm -f $(CSPROJ)

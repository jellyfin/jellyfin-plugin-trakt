.PHONY: build
build:
	docker build -t $(USERNAME)/jellyfin-plugin-trakt .
	docker run -v $(PWD)/plugins:/mnt $(USERNAME)/jellyfin-plugin-trakt \
		cp -a /plugins/Microsoft.Extensions.Logging.dll \
			/plugins/Trakt.dll /mnt/

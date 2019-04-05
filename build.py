#!/usr/bin/env python3
#! python3

import os, yaml, subprocess

def load_buildcfg(build_file):
    with open (build_file, 'r') as buildf:
        try:
            cfg = yaml.load(buildf)
        except yaml.YAMLError as e:
            print("ERROR: failed to load YAML manifest {}: {}".format(build_file, e))
            exit(1)
    return cfg

# Import build.yaml
build_file = "build.yaml"
cfg = load_buildcfg(build_file)

# Prepare the csproj file based on build.yaml
make_cmd = "make -f csproj.make csproj CSPROJ={} DOTNET_FRAMEWORK={} VERSION={} JELLYFIN_VERSION={}".format(
    cfg['csproj'],
    cfg['dotnet_framework'],
    cfg['version'],
    cfg['jellyfin_version']
)
subprocess.run(make_cmd.split())

# Publish with dotnet to ./bin
publish_cmd = "dotnet publish --configuration {} --framework {} --output {}/bin/".format(
    cfg['dotnet_configuration'],
    cfg['dotnet_framework'],
    os.getcwd(),
)
subprocess.run(publish_cmd.split())

# Clean up the csproj file
clean_cmd = "make -f csproj.make clean CSPROJ={}".format(
    cfg['csproj']
)
subprocess.run(clean_cmd.split())

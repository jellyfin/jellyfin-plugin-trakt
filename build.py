#!/usr/bin/env python3
#! python3

import os, yaml, subprocess, string

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
csproj_template_file = "csproj.in"
newcsproj = str()
with open(csproj_template_file, 'r') as csprojtf:
    for line in csprojtf.readlines():
        template = string.Template(line)
        newcsproj += template.substitute(
            DOTNET_FRAMEWORK=cfg['dotnet_framework'],
            PLUGIN_VERSION=cfg['version'],
            JELLYFIN_VERSION=cfg['jellyfin_version']
        )
print(newcsproj)
with open(cfg['csproj'], 'w') as csprojf:
    csprojf.write(newcsproj)

# Publish with dotnet to ./bin
publish_cmd = "dotnet publish --configuration {} --framework {} --output {}/bin/".format(
    cfg['dotnet_configuration'],
    cfg['dotnet_framework'],
    os.getcwd(),
)
subprocess.run(publish_cmd.split())

# Renove the csproj
os.remove(cfg['csproj'])

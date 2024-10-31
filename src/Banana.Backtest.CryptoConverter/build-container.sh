#!/bin/sh

dotnet publish \
-p PublishProfile=DefaultContainer \
-p ContainerImageTags='"1.0.0-alpha;latest"' \
-p ContainerFamily=alpine \
-p ContainerRuntimeIdentifier=linux-arm64 \
-p ContainerRepository=aroman35/banana-crypto-converter
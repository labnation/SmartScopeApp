#!/bin/bash -e

if [[ x$1 = x ]]; then
  echo Usage: $0 major.minor.build
  exit -1;
fi

GIT_TAG_PREFIX=release/
GIT_TAG=`git tag -l ${GIT_TAG_PREFIX}* --contains`

if [ ! -z ${GIT_TAG} ]; then
  echo Please, do not tag a commit you have already tagged before
  echo
  echo Seriously.
  exit -1
fi

TAG=${GIT_TAG_PREFIX}$1.0
MODULES_TO_TAG=( decoders DeviceInterface . )
CUR_DIR=`pwd`
for i in "${MODULES_TO_TAG[@]}"; do
  cd ${CUR_DIR}/$i
  TAG_EXISTS=`git tag -l --contains HEAD $TAG`
  #only add tag if it's not there yet
  if [[ x$TAG_EXISTS == x ]]; then
    git tag -a $TAG -m "Release $1"
  fi
  git push --tags origin
done
cd $CUR_DIR

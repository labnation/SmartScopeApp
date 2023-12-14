#!/bin/bash -e
SCRIPT_PATH=`pwd`/`dirname $0`
CONTENT_DIR=./Content
cd ${SCRIPT_PATH}/../${CONTENT_DIR}
find ./ -name '*.png' -exec git checkout -- '{}' ';'

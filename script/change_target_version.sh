#!/bin/bash -ex

SCRIPT=`basename $0`
SCRIPT_PATH=`pwd`/`dirname $0`
ROOT=${SCRIPT_PATH}/..

function usage {
    cat <<EOF
Usage:

$SCRIPT [options]
   -o|--os <operating system>
   -v|--version <version>
   -f <file>
   

Where:
       os       : Windows, MacOS, Linux, Android, iOS
       version  : New .NET target version / Android API

EOF
    exit -1
}

OPSYS=""
VERSION=""

while [ $# -gt 0 ]
do
key="$1"

case $key in
    -o|--os)
    OPSYS="$2"
    shift
    ;;
    -v|--version)
    VERSION="$2"
    shift
    ;;
    -f|--file)
    FILE="$2"
    shift
    ;;
    *)
    echo Unknown option $key
    usage
    ;;
esac
shift
done

#############################
# SETUP
#############################

if [ -z $OPSYS ] || [ -z $VERSION ]; then
    usage
fi

BUILD_DEFINITION_FILEMATCH='\*Build/Projects/*.definition'
if [ ${OPSYS} = "All" ]; then
    OPSYS_BUILD_DEF=[a-zA-Z]*
else
    OPSYS_BUILD_DEF=${OPSYS}
fi
BUILD_DEFINITION_PATTERN="(<Platform Name=\"${OPSYS_BUILD_DEF}\">\s*\n?\s*<Version>)v([0-9]+\.?)*(<\/Version>)"
CSPROJ_PATTERN="(<TargetFrameworkVersion>)v([0-9]+\.?)*(<\/TargetFrameworkVersion>)"
REPLACE="\1v${VERSION}\3"

find . -path \*Build/Projects/*.definition -exec perl -i -pe "BEGIN{undef $/;} s/$BUILD_DEFINITION_PATTERN/$REPLACE/g" {} \;
if [ ${OPSYS} = "All" ]; then
    find . -path \*.csproj -exec perl -i -pe "s/$CSPROJ_PATTERN/$REPLACE/g" {} \;
else
    find . -path \*${OPSYS}.csproj -exec perl -i -pe "s/$CSPROJ_PATTERN/$REPLACE/g" {} \;
fi

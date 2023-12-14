#!/bin/bash -ex

SCRIPT=`basename $0`
SCRIPT_PATH=`pwd`/`dirname $0`
ROOT=${SCRIPT_PATH}/..

function usage {
    cat <<EOF
Usage:

$SCRIPT [options]
   -o|--os <operating system>
   -c|--channel <channel>


Where:
       os       : Windows, MacOS, Linux, Android, iOS
       channel  : Release, Unstable, None

Options
    -clean  : Clean submodules
    -noregen: don't regenerate solution
    -nonuget: don't restore nuget packages
    -nobuild: don't build

EOF
    exit -1
}

OPSYS=""
DOCLEAN=false
CHANNEL=""
DOREGEN=true
DONUGET=true
DOBUILD=true

while [ $# -gt 0 ]
do
key="$1"

case $key in
    -o|--os)
    OPSYS="$2"
    shift
    ;;
    -c|--channel)
    CHANNEL="$2"
    shift
    ;;
    -clean)
    DOCLEAN=true
    ;;
    -noregen)
    DOREGEN=false
    ;;
    -nobuild)
    DOBUILD=false
    ;;
    -nonuget)
    DONUGET=false
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

cd "${ROOT}"

if [ -z $OPSYS ] || [ -z $CHANNEL ]; then
    usage
fi

# On mac we can't call the .exe's straight away
HOST=`uname`
if [[ $HOST != MINGW* ]] && [[ $HOST != MSYS_NT* ]]; then
  CMD_PREFIX="mono ./"
else
  CMD_PREFIX="./"
fi

# The general build target
TARGET=Build
# The solution configurationt to build
CONFIGURATION=Release
# Where the build will reside
if [ $OPSYS = 'iOS' ]; then
  PLATFORM=iPhone
else
  PLATFORM=AnyCPU
fi

OUTPUT_DIR=bin/$OPSYS/${PLATFORM}/${CONFIGURATION}/

# Where the distributable packaging happens and distributables will end up
DISTRIBUTION_DIR=./dist
PACKAGE_DIR=${DISTRIBUTION_DIR}/package/$OPSYS
BIN_DIR=${DISTRIBUTION_DIR}/bin

# iOS specifics
IOS_APP_NAME=SmartScope.app
IOS_PACKAGE_NAME=com.lab-nation.smartscope
IOS_PACKAGE_TEMPLATE=${DISTRIBUTION_DIR}/template/iOS
IOS_DEB_WORK_DIR=${PACKAGE_DIR}/${IOS_PACKAGE_NAME}
IOS_DPKG_CMD=${BIN_DIR}/dpkg-deb-fat
IOS_REPO_DIR=~/labnation/ios-repo

# Android specfics
APK_FILENAME=com.lab_nation.smartscope.apk
ZIPALIGN=zipalign
KEYSTORE=keystore/labnation.keystore
KEYSTORE_PASS=xeoPhah2

# Windows
WINSETUP_FOLDER=./Installer/$CONFIGURATION
WINSETUP_FILENAME=${WINSETUP_FOLDER}/smartscope.msi
if [[ $HOST == MINGW64* ]] || [[ $HOST == MSYS_NT* ]] ; then
  DEVENV="/c/Program Files (x86)/Microsoft Visual Studio/2017/Community/Common7/IDE/devenv.com"
  DISABLE_OUT_OF_PROC_BUILD="reg add HKEY_CURRENT_USER\Software\Microsoft\VisualStudio\15.0_816b276a_Config\MSBuild -v EnableOutOfProcBuild -t REG_DWORD -d 0 -f"
else
  DEVENV="/c/Program Files/Microsoft Visual Studio 14.0/Common7/IDE/devenv.com"
  DISABLE_OUT_OF_PROC_BUILD="reg add HKEY_CURRENT_USER\Software\Microsoft\VisualStudio\14.0_Config\MSBuild -v EnableOutOfProcBuild -t REG_DWORD -d 0 -f"
fi


# MacOS specifics
DMG_TEMPLATE=${DISTRIBUTION_DIR}/template/MacOS
DMG_FILENAME=smartscope.dmg
DMG_WORK_DIR=${PACKAGE_DIR}/dmgContents
VSTOOL="/Applications/Visual Studio.app/Contents/MacOS/vstool"

# Linux specifics
LINUX_PACKAGE_NAME=SmartScope
LINUX_DEB_TEMPLATE=${DISTRIBUTION_DIR}/template/Linux
LINUX_DEB_WORK_DIR=${PACKAGE_DIR}/${LINUX_PACKAGE_NAME}
LINUX_BINARY_PATH=./opt/smartscope
LINUX_DPKG_CMD=dpkg-deb

# Server variables
SERVER_NAME=SmartScopeServerUI
SERVER_ICON=smartscopeserver.ico
SERVER_DIR=DeviceInterface/examples/$SERVER_NAME
SERVER_OUTPUT_DIR=$SERVER_DIR/bin/$OPSYS/$PLATFORM/$CONFIGURATION
SERVER_CONSOLE_NAME=SmartScopeServer
SERVER_CONSOLE_OUTPUT_DIR=DeviceInterface/examples/$SERVER_CONSOLE_NAME/bin/$OPSYS/$PLATFORM/$CONFIGURATION

###########################
# BUILD!
###########################

# Clean the working tree
if [ $DOCLEAN = true ]; then
  echo Cleaning working tree
  git clean -f
  git reset --hard
  git submodule sync
  git submodule foreach --recursive git clean -f
  git submodule foreach --recursive git reset --hard
  git submodule foreach --recursive git submodule sync
  git submodule update --init --recursive --force
fi

if [ $DOREGEN = true ]; then
# Build project and solution files
${SCRIPT_PATH}/regenerate_solution.sh -o $OPSYS -c $CHANNEL
fi

VERSION_STRING=`cat version.txt`

if [ $DONUGET = true ]; then
# Make sure all nuget packages are up to date
${CMD_PREFIX}.nuget/NuGet.exe restore SmartScope.$OPSYS.sln
fi

## Workaround nuget not restoring DeviceInterface modules
if [ $OPSYS = "Windows" ] ||
   [ $OPSYS = "Linux" ] ||
   [ $OPSYS = "MacOS" ]; then
  cd DeviceInterface
  ./bootstrap.sh $OPSYS
  cd "${ROOT}"
fi

# Make sure the deploy directory exists
if [ ! -d ${DISTRIBUTION_DIR} ]; then
  echo Creating distribution directory ${DISTRIBUTION_DIR}
  mkdir -p ${DISTRIBUTION_DIR};
fi

if [ $OPSYS = "Windows" ] || [ $OPSYS = "WindowsGL" ]; then
  echo Ensuring registry key for building vdproj from command line
  ${DISABLE_OUT_OF_PROC_BUILD}
  echo Cleaning out installer folder
  rm -rf ${WINSETUP_FOLDER}
  if [ ${DOCLEAN} = true ]; then
    "${DEVENV}" SmartScope.$OPSYS.sln -clean "${CONFIGURATION}"
  fi
  if [ $DOBUILD = true ]; then
    "${DEVENV}" SmartScope.$OPSYS.sln -build "${CONFIGURATION}"
  fi
  echo Copying setup file to distribution directory
  mkdir -p ${PACKAGE_DIR}
  RELEASE_FILE=${PACKAGE_DIR}/smartscope-setup.msi
  cp ${WINSETUP_FILENAME} ${RELEASE_FILE}
elif [ $OPSYS = "iOS" ]; then
  if [ ${DOCLEAN} = true ]; then
    "${VSTOOL}" build -c:${CONFIGURATION}\|${PLATFORM} SmartScope.iOS.sln -t:Clean
  fi
  #unlock login keychain
  security unlock-keychain -p "${KEYCHAIN_PASSWORD}" ~/Library/Keychains/login.keychain
  if [ $DOBUILD = true ]; then
    "${VSTOOL}" build -c:${CONFIGURATION}\|${PLATFORM} SmartScope.iOS.sln -t:Build
  fi
  RELEASE_FILE=${OUTPUT_DIR}/labnation_smartscope.ipa
elif [ $OPSYS = "MacOS" ]; then
  if [ ${DOCLEAN} = true ]; then
    "${VSTOOL}" build SmartScope.$OPSYS.sln -c:${CONFIGURATION} -t:Clean
  fi
  if [ $DOBUILD = true ]; then
    "${VSTOOL}" build SmartScope.$OPSYS.sln -c:${CONFIGURATION} -t:${TARGET}
  fi
  echo Cleaning DMG folder
  mkdir -p ${DMG_WORK_DIR}
  rm -rf ${DMG_WORK_DIR}
  echo Copying over DMG template
  cp -R ${DMG_TEMPLATE} ${DMG_WORK_DIR}
  echo Moving the app into the DMG folder
  cp -r ${OUTPUT_DIR}/SmartScope.app ${DMG_WORK_DIR}
  #FIXME: Xamarin fails to copy the Bonjour.dll.config automatically :-(
  echo Doing the Bonjour.dll.config thing
  cp DeviceInterface/Mono.Zeroconf.Providers.Bonjour.dll.config ${SERVER_OUTPUT_DIR}/${SERVER_NAME}.app/Contents/MonoBundle
  echo Moving the Server to the DMG folder
  cp -r ${SERVER_OUTPUT_DIR}/${SERVER_NAME}.app ${DMG_WORK_DIR}
  RELEASE_FILE=${PACKAGE_DIR}/${DMG_FILENAME}
  echo Creating DMG ${RELEASE_FILE}
  hdiutil create -volname SmartScope -size 400M -srcfolder ${DMG_WORK_DIR} -ov -fs HFS+ -format UDZO ${RELEASE_FILE}
  echo Cleaning out DMG work dir folder
#  rm -rf ${DMG_WORK_DIR}
elif [ $OPSYS = "Linux" ]; then
  if [ ${DOCLEAN} = true ]; then
    xbuild SmartScope.$OPSYS.sln /t:Clean /p:Configuration=${CONFIGURATION}
  fi
  if [ $DOBUILD = true ]; then
    xbuild SmartScope.$OPSYS.sln /t:${TARGET} /p:Configuration=${CONFIGURATION}
  fi
  echo Copying over SmartScopeServerUI and SmartScopeServer to output dir
  cp ${SERVER_OUTPUT_DIR}/${SERVER_NAME}.exe ${OUTPUT_DIR}
  cp ${SERVER_DIR}/${SERVER_ICON} ${OUTPUT_DIR}
  cp ${SERVER_CONSOLE_OUTPUT_DIR}/${SERVER_CONSOLE_NAME}.exe ${OUTPUT_DIR}
  #Clean deb file work dir
  mkdir -p ${LINUX_DEB_WORK_DIR}
  rm -rf ${LINUX_DEB_WORK_DIR}
  #copy over template
  cp -r ${LINUX_DEB_TEMPLATE} ${LINUX_DEB_WORK_DIR}
  #copy build artifcats to work dir
  mkdir -p ${LINUX_DEB_WORK_DIR}/${LINUX_BINARY_PATH}
  mv ${OUTPUT_DIR}/* ${LINUX_DEB_WORK_DIR}/${LINUX_BINARY_PATH}
  #compute size
  PACKAGE_SIZE=`du -cs ${LINUX_DEB_WORK_DIR} | grep total | sed "s/[^0-9]//g"`
  echo Inserting package size of ${PACKAGE_SIZE} into debian control file
  sed -i~ "s/InstalledSizePlaceHolder/${PACKAGE_SIZE}/" ${LINUX_DEB_WORK_DIR}/DEBIAN/control
  #set deb package version
  sed -i~ -e "s/^Version: VersionPlaceHolder$/Version: ${VERSION_STRING}/" ${LINUX_DEB_WORK_DIR}/DEBIAN/control
  fakeroot ${LINUX_DPKG_CMD} -b ${LINUX_DEB_WORK_DIR}
  RELEASE_FILE=${LINUX_DEB_WORK_DIR}.deb
elif [ $OPSYS = "Android" ]; then
  if [ ${DOCLEAN} = true ]; then
    "${VSTOOL}" build SmartScope.$OPSYS.sln -c:${CONFIGURATION} -t:Clean
  fi
  if [ $DOBUILD = true ]; then
    "${VSTOOL}" build SmartScope.$OPSYS.sln -c:${CONFIGURATION} -t:${TARGET}
    "${VSTOOL}" archive SmartScope.$OPSYS.sln -c:${CONFIGURATION} || true
  fi
  jarsigner -verbose -sigalg SHA1withRSA -digestalg SHA1 -keystore ${KEYSTORE} -storepass ${KEYSTORE_PASS} ${OUTPUT_DIR}/${APK_FILENAME} labnation
  mkdir -p ${PACKAGE_DIR}
  RELEASE_FILE=${PACKAGE_DIR}/${APK_FILENAME}
  echo Zip aligning .apk
  ${ZIPALIGN} -f -v 4 ${OUTPUT_DIR}/${APK_FILENAME} ${RELEASE_FILE}
else
  echo Unknown OS $OPSYS
  exit -1;
fi

RELEASE_CMD="${SCRIPT_PATH}/release_package.sh SmartScope $OPSYS ${VERSION_STRING} ${RELEASE_FILE} $CHANNEL"
if [ $CHANNEL != "None" ]; then
  echo ${RELEASE_CMD}
  ${RELEASE_CMD}
else
  BORDER='########################################################'
  LINE='#                                                      #'
  echo -e "${BORDER}"
  echo -e "${LINE}"
  echo -e "${LINE}\r#                BUILD COMPLETE"
  echo -e "${LINE}"
  echo -e "${BORDER}"
  echo -e "${LINE}"
  echo -e "${LINE}\r# To release this build, run"
  echo -e "${LINE}"
  echo -e "${LINE}\r# ${RELEASE_CMD}"
  echo -e "${LINE}"
  echo -e "${BORDER}"
  echo
fi

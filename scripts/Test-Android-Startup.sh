#!/usr/bin/env bash
set -euo pipefail

apk="${1:-downloads/android/MDReader.apk}"
output="artifacts/android-startup"
package="com.pedrohda.mdreader"
mkdir -p "$output"
adb install -r "$apk"
adb logcat -c
adb shell am start -W -n "$package/$package.MainActivity"
sleep 12
adb logcat -d > "$output/logcat.txt"
adb logcat -d -b crash > "$output/crash.txt"
adb exec-out screencap -p > "$output/screen.png"
if ! adb shell pidof "$package"; then
    cat "$output/crash.txt"
    echo "Application exited during startup." >&2
    exit 1
fi
adb shell uiautomator dump /sdcard/mdreader-window.xml
adb pull /sdcard/mdreader-window.xml "$output/window.xml"
if ! grep -q "$package:id/open_file" "$output/window.xml"; then
    cat "$output/crash.txt"
    echo "Main interface did not become available." >&2
    exit 1
fi
adb shell input keyevent KEYCODE_HOME
sleep 2
adb shell am force-stop "$package"
adb shell am start -W -n "$package/$package.MainActivity"
sleep 5
adb logcat -d > "$output/logcat.txt"
adb logcat -d -b crash > "$output/crash.txt"
if ! adb shell pidof "$package"; then
    cat "$output/crash.txt"
    echo "Application exited after reopening." >&2
    exit 1
fi
echo "MDReader startup passed."

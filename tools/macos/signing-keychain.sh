#!/usr/bin/env bash
# The keychain that signs and notarizes a Mac package, made for one CI run and removed at its end. Run it on a macOS runner:
#
#   bash tools/macos/signing-keychain.sh create   (needs the five secrets below, as environment variables, never as arguments)
#   bash tools/macos/signing-keychain.sh delete   (safe to run when create failed halfway)
#
#   MACOS_CERTIFICATE_P12_BASE64  the Developer ID Application certificate and its private key, as a .p12, in base64
#   MACOS_CERTIFICATE_PASSWORD    the password of that .p12
#   APPLE_API_KEY_P8_BASE64       the App Store Connect API key (.p8), in base64
#   APPLE_API_KEY_ID              its key id
#   APPLE_API_ISSUER_ID           the issuer id of the team
#
# create leaves two variables for the later steps of the job (in $GITHUB_ENV): SCION_KEYCHAIN, the keychain's path, and
# SCION_SIGN_IDENTITY, the hash of the one Developer ID Application identity in it. The keychain's password is made here, from random
# bytes, and masked in the log: nothing has to keep it. Nothing here prints a secret; the secret files exist only while they are read.
set -euo pipefail
umask 077

work="${RUNNER_TEMP:?RUNNER_TEMP is not set: this script is for a CI runner.}/scion-signing"
keychain="$work/scion-signing.keychain-db"
search_list="$work/search-list.txt"

restore_search_list() {
  [ -f "$search_list" ] || return 0
  set --
  while IFS= read -r path; do
    if [ -n "$path" ]; then set -- "$@" "$path"; fi
  done < "$search_list"
  if [ "$#" -gt 0 ]; then security list-keychains -d user -s "$@"; fi
}

create() {
  : "${MACOS_CERTIFICATE_P12_BASE64:?is not set}" "${MACOS_CERTIFICATE_PASSWORD:?is not set}"
  : "${APPLE_API_KEY_P8_BASE64:?is not set}" "${APPLE_API_KEY_ID:?is not set}" "${APPLE_API_ISSUER_ID:?is not set}"

  mkdir -p "$work"
  echo "SCION_KEYCHAIN=$keychain" >> "${GITHUB_ENV:?GITHUB_ENV is not set: this script is for a CI runner.}"

  keychain_password=$(openssl rand -base64 32)
  echo "::add-mask::$keychain_password"

  security list-keychains -d user | tr -d '"' | sed 's/^ *//' > "$search_list"
  security create-keychain -p "$keychain_password" "$keychain"
  security set-keychain-settings -lut 21600 "$keychain"
  security unlock-keychain -p "$keychain_password" "$keychain"

  base64 --decode <<< "$MACOS_CERTIFICATE_P12_BASE64" > "$work/certificate.p12"
  security import "$work/certificate.p12" -k "$keychain" -P "$MACOS_CERTIFICATE_PASSWORD" -T /usr/bin/codesign -f pkcs12
  rm -f "$work/certificate.p12"
  security set-key-partition-list -S apple-tool:,apple: -s -k "$keychain_password" "$keychain" > /dev/null

  # Search the new keychain first, so codesign and notarytool find it; the old list comes back in delete.
  set -- "$keychain"
  while IFS= read -r path; do
    if [ -n "$path" ]; then set -- "$@" "$path"; fi
  done < "$search_list"
  security list-keychains -d user -s "$@"

  identities=$(security find-identity -v -p codesigning "$keychain" | grep "Developer ID Application" || true)
  found=$(printf '%s\n' "$identities" | grep -c . || true)
  if [ "$found" -ne 1 ]; then
    echo "Expected exactly one Developer ID Application identity in the certificate, found $found." >&2
    exit 1
  fi
  identity=$(awk '{ print $2 }' <<< "$identities")
  echo "::add-mask::$identity"
  echo "SCION_SIGN_IDENTITY=$identity" >> "$GITHUB_ENV"

  base64 --decode <<< "$APPLE_API_KEY_P8_BASE64" > "$work/api-key.p8"
  xcrun notarytool store-credentials scion-notary --key "$work/api-key.p8" --key-id "$APPLE_API_KEY_ID" --issuer "$APPLE_API_ISSUER_ID" --keychain "$keychain" > /dev/null
  rm -f "$work/api-key.p8"
}

# Every step runs even when an earlier one fails: the files must go whatever happens.
delete() {
  restore_search_list || echo "The keychain search list could not be restored." >&2
  if [ -f "$keychain" ]; then security delete-keychain "$keychain" || echo "The signing keychain could not be deleted." >&2; fi
  rm -rf "$work"
}

case "${1:-}" in
  create) create ;;
  delete) delete ;;
  *)
    echo "Usage: signing-keychain.sh create|delete" >&2
    exit 2
    ;;
esac

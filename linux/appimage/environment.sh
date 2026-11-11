export LD_LIBRARY_PATH="$APPDIR/usr/lib:$APPDIR/usr/lib/twitch-chat-window${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
source "$APPDIR/usr/share/twitch-chat-window/runtime.env"
export DOTNET_SYSTEM_GLOBALIZATION_APPLOCALICU="$APPIMAGE_ICU_MAJOR"
export DOTNET_OPENSSL_VERSION_OVERRIDE=3
if [[ -z ${SSL_CERT_FILE:-} ]]; then
    for certificates in /etc/ssl/certs/ca-certificates.crt /etc/ssl/cert.pem /etc/pki/tls/certs/ca-bundle.crt; do
        if [[ -r "$certificates" ]]; then
            export SSL_CERT_FILE="$certificates"
            break
        fi
    done
fi

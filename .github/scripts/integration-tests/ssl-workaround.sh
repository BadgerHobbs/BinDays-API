#!/bin/bash
set -e

# Workaround for SSL error on self-hosted runner
#
# This script configures OpenSSL to allow legacy TLS versions for councils
# that use older TLS configurations (e.g., Teignbridge).
#
# The error "The SSL connection could not be established" is caused by
# the council's server using a legacy TLS version. This updates the SSL
# config to allow for a less strict security level.
#
# The script is idempotent and will only modify the openssl.cnf file
# if the required configuration is not already present.

if ! grep -q "ssl_conf = ssl_sect" /etc/ssl/openssl.cnf; then
  sudo sed -i '/\[openssl_init\]/a ssl_conf = ssl_sect' /etc/ssl/openssl.cnf
fi

if ! grep -q "\[ssl_sect\]" /etc/ssl/openssl.cnf; then
  echo -e "\n[ssl_sect]\nsystem_default = system_default_sect\n\n[system_default_sect]\nMinProtocol = TLSv1\nCipherString = DEFAULT@SECLEVEL=1" | sudo tee -a /etc/ssl/openssl.cnf
fi

echo "SSL configuration updated for legacy TLS support"

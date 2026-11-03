# syntax=docker/dockerfile:1.7
# KoSIT-Validator (Apache-2.0) im Daemon-Modus mit der offiziellen XRechnung-Konfiguration (Apache-2.0).
# Beide Dateien kommen aus den GitHub-Releases und werden per SHA-256 geprüft.
# Für ein Update: Version, Dateiname und Prüfsumme anpassen (Prüfsumme steht im jeweiligen GitHub-Release).

FROM busybox:1.37 AS download
ADD --checksum=sha256:799e64befca97d4080e03608c80b85dd5a5ecc5f4ae4f35d1116ec2855b9a7c9 \
    https://github.com/itplr-kosit/validator/releases/download/v1.6.3/validator-1.6.3-standalone.jar \
    /opt/validator/validator.jar
ADD --checksum=sha256:2530cd107c414511c5d0462ec10f886910395abfca820db82e83d70bf01221a8 \
    https://github.com/itplr-kosit/validator-configuration-xrechnung/releases/download/v2026-08-31/xrechnung-3.0.2-validator-configuration-2026-08-31.zip \
    /tmp/configuration.zip
RUN mkdir -p /opt/validator/configuration \
    && unzip -q /tmp/configuration.zip -d /opt/validator/configuration \
    && test -f /opt/validator/configuration/scenarios.xml \
    && chmod -R a+rX /opt/validator

FROM eclipse-temurin:21-jre
RUN useradd --system --uid 10001 --no-create-home --shell /usr/sbin/nologin validator
COPY --from=download /opt/validator /opt/validator
USER 10001
EXPOSE 8080
# -D Daemon, -G ohne Weboberfläche: nur die Prüf-Schnittstelle (POST /) wird angeboten.
ENTRYPOINT ["java", "-XX:MaxRAMPercentage=75", "-jar", "/opt/validator/validator.jar", \
            "-s", "/opt/validator/configuration/scenarios.xml", "-r", "/opt/validator/configuration", \
            "-D", "-G", "-H", "0.0.0.0", "-P", "8080"]

# Minimal maintenance-page container for the RSGO Maintenance Edge-Proxy.
# Serves the static page in ./html on port 80. The edge reverse-proxies to it
# during maintenance (maintenancePage.mode: container).
#
# Build & push:
#   docker build -t your-registry/ams-maintenance-page:1.0 .
#   docker push your-registry/ams-maintenance-page:1.0
FROM nginx:alpine
COPY html/ /usr/share/nginx/html/
EXPOSE 80

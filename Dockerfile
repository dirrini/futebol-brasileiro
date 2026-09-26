FROM nginx:stable-alpine@sha256:985220252f3863977e468f611ef118ebd01421289dd86ee1ae99cb068c3bce2b

RUN rm -f /usr/share/nginx/html/index.html /usr/share/nginx/html/50x.html
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY Builds/WebGL/ /usr/share/nginx/html/
RUN test -s /usr/share/nginx/html/index.html

EXPOSE 80

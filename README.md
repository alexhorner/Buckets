# Buckets
## A frighteningly simple bucketing API without a database backend

---

Buckets is a super simple file bucketing API without any kind of database backend. It was born out of my own dissatisfaction with existing self-hostable object storage servers.

I would not recommend this for production use unless you do your own full code review!

---

Make sure to adjust the configuration before deployment!
```json
{
    "Logging": {
        "LogLevel": {
            "Default": "Information",
            "Microsoft": "Warning",
            "Microsoft.Hosting.Lifetime": "Information"
        }
    },
    "AllowedHosts": "*",
    "UseForwardedHeaders": false,
    "UseHttpsRedirection": true,
    "EnableSwaggerDocumentation": false,
    "BucketPath": "./buckets",
    "AuthenticationRequirements": {
        "BucketList": true,
        "ObjectList": true,
        "ObjectRead": true,
        "ObjectCreate": true,
        "ObjectDelete": true
    },
    "AuthenticationKeys": [
        "CHANGE_ME"
    ]
}
```

---

## SystemD
Buckets has support for SystemD enabled by default for the Hosting model. Simply create a unit file like so:
`/etc/systemd/system/buckets.service`
```ini
[Unit]
Description=Buckets Server - A frighteningly simple bucketing API without a database backend

[Service]
Type=notify
WorkingDirectory=/opt/buckets
ExecStart=/usr/bin/dotnet /opt/buckets/Buckets.Web.dll --urls http://127.0.0.1:6000;https://127.0.0.1:6001
Restart=always
RestartSec=10
User=www-data

[Install]
WantedBy=multi-user.target
```
Please note the semicolon separated URLs may syntax highlight as an INI comment, but they are not a comment and are interpreted properly.

You may adjust the `ExecStart` for the dotnet location and app ports, and the working direcory should match the place of the `ExecStart` for the `Buckets.Web.dll` as shown above. You may also need to adjust the user if the default `www-data` user is not available on your system. You should not run Buckets under the root user. Preferrably, Buckets should run under its very own user.

---

## TODO
- Caching maybe?
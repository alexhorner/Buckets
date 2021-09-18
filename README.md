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

## TODO
- Caching maybe?
- Better checks around bad user input!
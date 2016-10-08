# Initial State C# Event Sender

This is a simple library example of sending data to Initial State's events API in C# with config file support. The plan is for this to be converted into a nuget project soon and published.

The configuration support allows you to set a configuration element in your app.config or web.config that looks like the following:

```
<configuration>
  <initialstate accessKey="YOUR_ACCESS_KEY_HERE" />
</configuration>
```


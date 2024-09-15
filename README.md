# NetEscapades.AspNetCore.SecurityHeaders

[![Build status](https://ci.appveyor.com/api/projects/status/q261l3sbokafmx1o/branch/develop?svg=true)](https://ci.appveyor.com/project/andrewlock/netescapades-aspnetcore-securityheaders/branch/master)
<!--[![Travis](https://img.shields.io/travis/andrewlock/NetEscapades.AspNetCore.SecurityHeaders.svg?maxAge=3600&label=travis)](https://travis-ci.org/andrewlock/NetEscapades.AspNetCore.SecurityHeaders)-->
[![NuGet](https://img.shields.io/nuget/v/NetEscapades.AspNetCore.SecurityHeaders.svg)](https://www.nuget.org/packages/NetEscapades.AspNetCore.SecurityHeaders/)
[![MyGet CI](https://img.shields.io/myget/andrewlock-ci/v/NetEscapades.AspNetCore.SecurityHeaders.svg)](http://myget.org/gallery/acndrewlock-ci)

A small package to allow adding security headers to ASP.NET Core websites

## Installing

Install using the [NetEscapades.AspNetCore.SecurityHeaders NuGet package](https://www.nuget.org/packages/NetEscapades.AspNetCore.SecurityHeaders) from the Visual Studio Package Manager Console:

```
PM> Install-Package NetEscapades.AspNetCore.SecurityHeaders
```

Or using the `dotnet` CLI

```bash
dotnet add package NetEscapades.AspNetCore.SecurityHeaders
```

## Usage

When you install the package, it should be added to your `.csproj`. Alternatively, you can add it directly by adding:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>netcoreapp5.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="NetEscapades.AspNetCore.SecurityHeaders" Version="0.24.0" />
  </ItemGroup>
  
</Project>
```

There are various ways to configure the headers for your application. 

In the simplest scenario, add the middleware to your ASP.NET Core application by configuring it as part of your normal `Startup` pipeline (or `WebApplication` in .NET 6+). Note that the order of middleware matters, so to apply the headers to all requests it should be configured first in your pipeline.

To use the default security headers for your application, add the middleware using:

```csharp
public void Configure(IApplicationBuilder app)
{
    app.UseSecurityHeaders();

    // other middleware e.g. static files, MVC etc  
}
```

This adds the following headers to all responses that pass through the middleware:

* `X-Content-Type-Options: nosniff`
* `Strict-Transport-Security: max-age=31536000; includeSubDomains` - _only applied to HTTPS responses_
* `X-Frame-Options: Deny` - _only applied to "document" responses_
* `X-XSS-Protection: 1; mode=block` - _only applied to "document" responses_
* `Referrer-Policy: strict-origin-when-cross-origin` - _only applied to "document" responses_
* `Content-Security-Policy: object-src 'none'; form-action 'self'; frame-ancestors 'none'` - _only applied to "document" responses_

"Document" responses are defined as responses that return one of the following content-types:

- `text/html`
- `text/javascript`
- `application/javascript`

## Customising the security headers added to responses

To customise the headers returned, you should create an instance of a `HeaderPolicyCollection` and add the required policies to it. There are helper methods for adding a number of security-focused header values to the collection, or you can alternatively add any header by using the `CustomHeader` type. For example, the following would set a number of security headers, and a custom header `X-My-Test-Header`. 

```csharp
public void Configure(IApplicationBuilder app)
{
    var policyCollection = new HeaderPolicyCollection()
        .AddFrameOptionsDeny()
        .AddXssProtectionBlock()
        .AddContentTypeOptionsNoSniff()
        .AddStrictTransportSecurityMaxAgeIncludeSubDomains(maxAgeInSeconds: 60 * 60 * 24 * 365) // maxage = one year in seconds
        .AddReferrerPolicyStrictOriginWhenCrossOrigin()
        .RemoveServerHeader()
        .AddContentSecurityPolicy(builder =>
        {
            builder.AddObjectSrc().None();
            builder.AddFormAction().Self();
            builder.AddFrameAncestors().None();
        })
        .AddCrossOriginOpenerPolicy(builder =>
        {
            builder.SameOrigin();
        })
        .AddCrossOriginEmbedderPolicy(builder =>
        {
            builder.RequireCorp();
        })
        .AddCrossOriginResourcePolicy(builder =>
        {
            builder.SameOrigin();
        })
        .AddCustomHeader("X-My-Test-Header", "Header value");

    app.UseSecurityHeaders(policyCollection);

    // other middleware e.g. static files, MVC etc  
}
```

The security headers above are also encapsulated in another extension method, so you could rewrite it more tersely using 

```csharp
public void Configure(IApplicationBuilder app)
{
    var policyCollection = new HeaderPolicyCollection()
        .AddDefaultSecurityHeaders()
        .AddCustomHeader("X-My-Test-Header", "Header value");

    app.UseSecurityHeaders(policyCollection);

    // other middleware e.g. static files, MVC etc  
}
```

If you want to use the default security headers, but change one specific header, you can simply add another header to the default collection. For example, the following uses the default headers, but changes the max-age on the `Strict-Transport-Security` header:

```csharp
public void Configure(IApplicationBuilder app)
{
    var policyCollection = new HeaderPolicyCollection()
        .AddDefaultSecurityHeaders()
        .AddStrictTransportSecurityMaxAgeIncludeSubDomains(maxAgeInSeconds: 63072000);

    app.UseSecurityHeaders(policyCollection);

    // other middleware e.g. static files, MVC etc  
}
```

There is also a convenience overload for `UseSecurityHeaders` that takes an `Action<HeaderPolicyCollection>`, instead of requiring you to instantiate a `HeaderPolicyCollection` yourself:

```csharp
public void Configure(IApplicationBuilder app)
{
    app.UseSecurityHeaders(policies =>
        policies
            .AddDefaultSecurityHeaders()
            .AddStrictTransportSecurityMaxAgeIncludeSubDomains(maxAgeInSeconds: 63072000)
    );

    // other middleware e.g. static files, MVC etc  
}
```

## Applying different headers to different endpoints

In some situations, you may need to apply different security headers to different endpoints. For example, you may want to have a very restrictive Content-Security-Policy by default, but then have a more relaxed on specific endpoints that require it. This is supported, but requires more configuration.

### 1. Configure your policies using `AddSecurityHeaderPolicies()`

You can configure named and default policies by calling `AddSecurityHeaderPolicies()` on `IServiceCollection`. You can configure the default policy to use, as well as any named policies. For example, the following configures the default policy (used for all requests that are not customised for an endpoint), and a named policy:

```csharp
var builder = WebApplication.CreateBuilder();

// 👇 Call AddSecurityHeaderPolicies()
builder.Services.AddSecurityHeaderPolicies()
    .SetDefaultPolicy(policy => policy.AddDefaultSecurityHeaders()) // 👈 Configure the default policy
    .AddPolicy("CustomPolicy", policy => policy.AddCustomHeader("X-Custom", "SomeValue")); // 👈 Configure named policies

```

### 2. Call `UseSecurityHeaders()` early in the middleware pipeline

The security headers middleware can only add headers to _all_ requests if it is early in the middleware pipeline, so it's important to add the headers middleware at the start of your middleware pipeline by calling `UseSecurityHeaders()`. For example:

```csharp
var builder = WebApplication.CreateBuilder();

// 👇 Configure policies as shown previously
builder.Services.AddSecurityHeaderPolicies()
    .SetDefaultPolicy(policy => policy.AddDefaultSecurityHeaders())
    .AddPolicy("CustomPolicy", policy => policy.AddCustomHeader("X-Custom", "SomeValue"));

var app = builder.Build();

// Add the middleware to the start of your pipeline
// 👇
app.UseSecurityHeaders();

app.UseStaticFiles(); // other middleware
app.UseAuthentication();
app.UseRouting(); 

app.UseAuthorization();

app.MapGet("/", () => "Hello world");
app.Run();
```

### 3. Apply custom policies to endpoints

To apply a non-default policy to an endpoint, use the `WithSecurityHeadersPolicy(policy)` endpoint extension method, and pass in the name of the policy to apply:

```csharp
var builder = WebApplication.CreateBuilder();

builder.Services.AddSecurityHeaderPolicies()
    .SetDefaultPolicy(policy => policy.AddDefaultSecurityHeaders())
    .AddPolicy("CustomPolicy", policy => policy.AddCustomHeader("X-Custom", "SomeValue"));

var app = builder.Build();

app.UseSecurityHeaders();

app.UseStaticFiles();
app.UseAuthentication();
app.UseRouting(); 

app.UseEndpointSecurityHeaders(); 
app.UseAuthorization();

app.MapGet("/", () => "Hello world")
    .WithSecurityHeadersPolicy("CustomPolicy"); // 👈 Apply a named policy to the endpoint 
app.Run();
```

If you're using MVC controllers or Razor Pages, you can apply the `[SecurityHeadersPolicy(policyName)]` attribute to your endpoints:

```csharp
public class HomeController : ControllerBase
{
    [SecurityHeadersPolicy("CustomHeader")] // 👈 Apply a custom header to the endpoint
    public IActionResult Index()
    {
        return View();
    }
}
```

Security headers are applied just before the response is sent. If you use the configuration described above, then the policy to apply is determined as follows, with the first applicable policy selected:

1. If an endpoint has been selected, and a named policy is applied, use that.
2. If a named or policy instance is passed to the `SecurityHeadersMiddleware()`, use that.
3. If the default policy has been set using `SetDefaultPolicy()`, use that.
4. Otherwise, apply the default headers (those added by `AddDefaultSecurityHeaders()`)

## Customizing the headers per request

If you need to use a different set of security headers for certain endpoints in your application, then configuring named policies  (as described)[#pplying-different-headers-to-different-endpoints] above is the best approach.

However, sometimes you need even more customization on a per-request basis. For example, perhaps you have a multi-tenant application, and you need to apply different headers based on a header in the request (or the response) that identifies the tenant. In this situation, you don't know at application startup which set of headers to apply. 

To customize the final `HeaderPolicyCollection` used for a request, you can use the `SetPolicySelector()` method available on `IServiceCollection.AddSecurityHeaderPolicies()`. This method take a `Func<>` argument which is passed a context object, and must return an `IReadOnlyHeaderPolicyCollection`. The `SetPolicySelector()` argument is invoked for every request, just before the final selected policy is applied, and allows you to change the `IReadOnlyHeaderPolicyCollection` to apply.

The following code shows how to use services in combination with the `SetPolicySelector()` method. This isn't necessary, but rather shows that you can completely customise the applied headers in any way you need.

```csharp
var builder = WebApplication.CreateBuilder();

// 👇 a custom service that selects the policy based on tenants
builder.Services.AddScoped<TenantHeaderPolicyCollectionSelector>(); 
builder.Services.AddSecurityHeaderPolicies()
    .AddPolicy(policyName, p => p.AddCustomHeader("Custom-Header", "MyValue"))
    .SetPolicySelector((PolicySelectorContext ctx) =>
    {
        // Use services from the DI container (if you need to)
        IServiceProvider services = ctx.HttpContext.RequestServices; 
        var selector = services.GetService<TenantHeaderPolicyCollectionSelector>();
        var tenant = services.GetService<ITenant>();
        return selector.GetPolicy(tenant);
    };
```

Note that you should avoid creating a `HeaderPolicyCollection` from scratch on each request. Instead, cache policies for multiple requests where possible. However, if you need to build a new policy based on the policy passed in the context object, you can create a mutable copy by calling `IReadOnlyHeaderPolicyCollection.Copy()`, adding/updating policies as required, and returning the `HeaderPolicyCollection`. 

## RemoveServerHeader

One point to be aware of is that the `RemoveServerHeader` method will rarely (ever?) be sufficient to remove the `Server` header from your output. If any subsequent middleware in your application pipeline add the header, then this will be able to remove it. However Kestrel will generally add the `Server` header too late in the pipeline to be able to modify it.

Luckily, Kestrel exposes it's own mechanism to allow you to prevent it being added:

```csharp
var host = new WebHostBuilder()
    .UseKestrel(options => options.AddServerHeader = false)
    //...
```

In `Program.cs`, when constructing your app's `WebHostBuilder`, configure the `KestrelServerOptions` to prevent the `Server` tag being added.

## AddContentSecurityPolicy

The `Content-Security-Policy` (CSP) header is a very powerful header that can protect your website from a wide range of attacks. However, it's also totally possible to create a CSP header that completely breaks your app. 

The CSP has a dizzying array of options, only some of which are implemented in this project. Consequently, I highly recommend reading [this post by Scott Helme](https://scotthelme.co.uk/content-security-policy-an-introduction/), in which he discusses the impact of each "directive". I also highly recommend using the "report only" version of the header when you start. This won't break your site, but will report instances that it would be broken, by providing reports to a service such as report-uri.com.

Set the header to report-only by using the `AddContentSecurityPolicyReportOnly()` extension. For example:

```csharp
public void Configure(IApplicationBuilder app)
{
    var policyCollection = new HeaderPolicyCollection()
        .AddContentSecurityPolicyReportOnly(builder => // report-only
        {
            // configure policies
        });
}
```

or by by passing `true` to the `AddContentSecurityPolicy` command

```csharp
public void Configure(IApplicationBuilder app)
{
    var policyCollection = new HeaderPolicyCollection()
        .AddContentSecurityPolicy(builder =>
        {
            // configure policies
        },
        asReportOnly: true); // report-only
}
```

You configure your CSP policy when you configure your `HeaderPolicyCollection` in `Startup.Configure`. For example:

```csharp
public void Configure(IApplicationBuilder app)
{
    var policyCollection = new HeaderPolicyCollection()
        .AddContentSecurityPolicy(builder =>
        {
            builder.AddUpgradeInsecureRequests(); // upgrade-insecure-requests
            builder.AddBlockAllMixedContent(); // block-all-mixed-content

            builder.AddReportUri() // report-uri: https://report-uri.com
                .To("https://report-uri.com");

            builder.AddDefaultSrc() // default-src 'self' http://testUrl.com
                .Self()
                .From("http://testUrl.com");

            builder.AddConnectSrc() // connect-src 'self' http://testUrl.com
                .Self()
                .From("http://testUrl.com");

            builder.AddFontSrc() // font-src 'self'
                .Self();

            builder.AddObjectSrc() // object-src 'none'
                .None();

            builder.AddFormAction() // form-action 'self'
                .Self();

            builder.AddImgSrc() // img-src https:
                .OverHttps();

            builder.AddScriptSrc() // script-src 'self' 'unsafe-inline' 'unsafe-eval' 'report-sample'
                .Self()
                .UnsafeInline()
                .UnsafeEval()
                .ReportSample();

            builder.AddStyleSrc() // style-src 'self' 'strict-dynamic'
                .Self()
                .StrictDynamic();

            builder.AddMediaSrc() // media-src https:
                .OverHttps();

            builder.AddFrameAncestors() // frame-ancestors 'none'
                .None();

            builder.AddBaseUri() // base-ri 'self'
                .Self();

            builder.AddFrameSource() // frame-src http://testUrl.com
                .From("http://testUrl.com");

            // You can also add arbitrary extra directives: plugin-types application/x-shockwave-flash"
            builder.AddCustomDirective("plugin-types", "application/x-shockwave-flash");

        })
        .AddCustomHeader("X-My-Test-Header", "Header value");

    app.UseSecurityHeaders(policyCollection);

    // other middleware e.g. static files, MVC etc  
}
```

## AddPermissionsPolicy

The `permissions-policy` is a header that allows a site to control which features and APIs can be used in the browser. It is similar to CSP but controls features instead of security behaviour.

With Permissions-Policy, you opt-in to a set of "policies" for the browser to enforce on specific features used throughout a website. These policies restrict what APIs the site can access or modify the browser's default behaviour for certain features.

By adding Permissions-Policy to headers to your website, you can ensure that sensitive APIs like geolocation or the camera cannot be used, even if your site is otherwise compromised, for example by malicious third-party attacks.

For more information about the permissions, I recommend the following resources:

* Scott Helme's introduction to Permissions-Policy: https://scotthelme.co.uk/goodbye-feature-policy-and-hello-permissions-policy/
* The list of policy-controlled permissions: https://www.w3.org/TR/permissions-policy-1/
* MDN documentation: https://developer.mozilla.org/en-US/docs/Web/HTTP/Feature_Policy
* Google's introduction to Feature-Policy: https://developers.google.com/web/updates/2018/06/feature-policy

You configure your CSP policy when you configure your `HeaderPolicyCollection` in `Startup.Configure`. For example:

```csharp
public void Configure(IApplicationBuilder app)
{
    var policyCollection = new HeaderPolicyCollection()
        .AddPermissionsPolicy(builder =>
        {
            builder.AddAccelerometer() // accelerometer 'self' http://testUrl.com
                .Self()
                .For("http://testUrl.com");

            builder.AddAmbientLightSensor() // ambient-light-sensor 'self' http://testUrl.com
                .Self()
                .For("http://testUrl.com");

            builder.AddAutoplay() // autoplay 'self'
                .Self();

            builder.AddCamera() // camera 'none'
                .None();

            builder.AddEncryptedMedia() // encrypted-media 'self'
                .Self();

            builder.AddFullscreen() // fullscreen *:
                .All();

            builder.AddGeolocation() // geolocation 'none'
                .None();

            builder.AddGyroscope() // gyroscope 'none'
                .None();

            builder.AddMagnetometer() // magnetometer 'none'
                .None();

            builder.AddMicrophone() // microphone 'none'
                .None();

            builder.AddMidi() // midi 'none'
                .None();

            builder.AddPayment() // payment 'none'
                .None();

            builder.AddPictureInPicture() // picture-in-picture 'none'
                .None();

            builder.AddSpeaker() // speaker 'none'
                .None();

            builder.AddSyncXHR() // sync-xhr 'none'
                .None();

            builder.AddUsb() // usb 'none'
                .None();

            builder.AddVR() // vr 'none'
                .None();

            // You can also add arbitrary extra directives: plugin-types application/x-shockwave-flash"
            builder.AddCustomFeature("plugin-types", "application/x-shockwave-flash");
            // If a new feature policy is added that follows the standard conventions, you can use this overload
            // iframe 'self' http://testUrl.com
            builder.AddCustomFeature("iframe") // 
                .Self()
                .For("http://testUrl.com");
        });

    app.UseSecurityHeaders(policyCollection);

    // other middleware e.g. static files, MVC etc  
}
```

## Using Nonces and generated-hashes with Content-Security-Policy

The use of a secure Content-Security-Policy can sometimes be problematic when you need to include inline-scripts, styles, or other objects that haven't been whitelisted. You can achieve this in two ways - using a "nonce" (or "number-used-once"), or specifying the hash of the content to include. 

To help with this you can install the NetEscapades.AspNetCore.SecurityHeaders.TagHelpers package, which provides helpers for generating a nonce per request, which is attached to the HTML element, and included in the CSP header. A similar method helper exists for `<style>` and `<script>` tags, which will take a SHA256 hash of the contents of the HTML element and add it to the CSP whitelist. For _inline styles_, and inline event handlers, there is an helper that supports generating hashes for the contents of such attribute.

To use a nonce or an auto-generated hash with your ASP.NET Core application, use the following steps.

### 1. Install the NetEscapades.AspNetCore.SecurityHeaders.TagHelpers NuGet package, e.g.

```bash
dotnet package add Install-Package NetEscapades.AspNetCore.SecurityHeaders.TagHelpers
```

This adds the package to your _.csproj_ file

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>netcoreapp3.1</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="NetEscapades.AspNetCore.SecurityHeaders" Version="0.24.0" />
    <PackageReference Include="NetEscapades.AspNetCore.SecurityHeaders.TagHelpers" Version="0.24.0" />
  </ItemGroup>
  
</Project>
```

### 2. Configure your CSP to use nonces and/or hashes

Configure your security headers in the usual way. Use the `WithNonce()` extension method when configuring  `ContentSecurityPolicy` directives to allow whitelisting with a nonce. Use the `WithHashTagHelper()` extension methods on `script-src` and `style-src` directives to allow automatic generation of whitelisted inline-scripts

```csharp
public void Configure(IApplicationBuilder app)
{
    var policyCollection = new HeaderPolicyCollection()
        .AddContentSecurityPolicy(builder =>
        {
            builder.AddUpgradeInsecureRequests(); 
            builder.AddDefaultSrc() // default-src 'self' http://testUrl.com
                .Self()
                .From("http://testUrl.com");

            builder.AddScriptSrc() // script-src 'self' 'unsafe-inline' 'nonce-<base64-value>'
                .Self()
                .UnsafeInline()
                .WithNonce(); // Allow elements marked with a nonce attribute

            builder.AddStyleSrc() // style-src 'self' 'strict-dynamic' 'sha256-<base64-value>'
                .Self()
                .StrictDynamic()
                .WithHashTagHelper().UnsafeHashes(); // Allow allowlisted elements based on their SHA256 hash value
        })
        .AddCustomHeader("X-My-Test-Header", "Header value");

    app.UseSecurityHeaders(policyCollection);

    // other middleware e.g. static files, MVC etc  
}
```

### 3. Add a using directive for the TagHelpers

Add the following to the *_ViewImports.cshtml* file in your application. This makes the tag-helper available in your Razor views. 

```csharp
@addTagHelper *, NetEscapades.AspNetCore.SecurityHeaders.TagHelpers
```

### 4. Whitelist elements using the TagHelpers

Add the `NonceTagHelper` to an element by adding the `asp-add-nonce` attribute.

```html
<script asp-add-nonce>
    var body = document.getElementsByTagName('body')[0];
    var div = document.createElement('div');
    div.innerText = "I was added using the NonceHelper";
    body.appendChild(div);
</script>
```

This will use a unique value per-request and attach the required attribute at runtime, to generate markup similar to the following:

```html
<script nonce="ryPzmoZScSR2xOwV0qTU9mFdFwGPN&#x2B;gy3S2E1/VK1vg=">
    var body = document.getElementsByTagName('body')[0];
    var blink = document.createElement('div');
    blink.innerText = "And I was added using the NonceHelper";
    body.appendChild(blink);
</script>
```

> Note that some browsers will hide the value of the `nonce` attribute when viewed from DevTools. View the page source to see the raw nonce value

While the CSP policy would look something like the following:

```http
Content-Security-Policy: script-src 'self' 'unsafe-inline' 'nonce-ryPzmoZScSR2xOwV0qTU9mFdFwGPN&#x2B;gy3S2E1/VK1vg='; style-src 'self' 'strict-dynamic'; default-src 'self' http://testUrl.com
```

To use a whitelisted hash instead, use the `HashTagHelper`, by adding the `asp-add-content-to-csp` attribute to `<script>` or `<style>` tags. You can optionally add the `csp-hash-type` attribute to choose between SHA256, SHA384, and SHA512:

```html
<script asp-add-content-to-csp>
    var msg = document.getElementById('message');
    msg.innerText = "I'm allowed";
</script>

<style asp-add-content-to-csp csp-hash-type="SHA384">
#message {
    color: @color;
}  
</style>
```

At runtime, these attributes are removed, but the hash values of the contents are added to the `Content-Security-Policy header`.

### 5. Whitelist attributes using the TagHelpers

Inline styles, and event handlers don't support nonces, but there is a dedicated tag helper that supports hashing attributes: `AttributeHashTagHelper`. This works similar to the tag helper for elements, but add the `asp-add-csp-for-*` attribute where `*` is the name of the attribute to hash, like this:

```html
<h3 asp-add-csp-for-style style="color: red">I will be styled red</h3>
```

Multiple occurrences of this attribute is supported using different values for `*` in case you need to hash multiple attributes. You can still set the hash type by setting that on the attribute `asp-add-csp-for-*` directly (SHA256, SHA384, and SHA512 are still the valid options here):

```html
<button asp-add-csp-for-style style="color: red" asp-add-csp-for-onclick="SHA384" onclick="alert('Hello!')">Click me!</button>
```

At runtime, these attributes are removed, but the hash values of the contents are added to the `Content-Security-Policy header`.

### Using the generated nonce without a TagHelpers

If you aren't using Razor, or don't want to use the TagHelpers library, you can access the Nonce for a request using an extension method on `HttpContext`:

```csharp
var nonce = HttpContext.GetNonce();
```

> Note that you must have enabled nonce generation by using the `WithNonce()` method. `HttpContext.GetNonce()` will return an `string.Empty` if nonce generation has not been added to the middleware.

## Additional Resources

* [ASP.NET Core Middleware Docs](https://docs.asp.net/en/latest/fundamentals/middleware.html)
* [How to add default security headers in ASP.NET Core using custom middleware](http://andrewlock.net/adding-default-security-headers-in-asp-net-core/)
* [Content Security Policy - An Introduction](https://scotthelme.co.uk/content-security-policy-an-introduction/) by Scott Helme
* [Content Security Policy Reference](https://content-security-policy.com/)
* [Content Security Policy (CSP)](https://developer.mozilla.org/en-US/docs/Web/HTTP/CSP) by Mozilla Developer Network

> Note, Building on Travis is currently disabled, due to issues with the mono framework. For details, see
> * http://stackoverflow.com/questions/42747722/building-vs-2017-msbuild-csproj-projects-with-mono-on-linux/42861338
> * https://github.com/dotnet/sdk/issues/335

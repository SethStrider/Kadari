var builder = DistributedApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Database — PostgreSQL + EF Core. Database/connection name: "kadaridb".
// ---------------------------------------------------------------------------
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume();

var kadariDb = postgres.AddDatabase("kadaridb");

// ---------------------------------------------------------------------------
// Blob storage — MinIO (S3-compatible). Every object is envelope-encrypted by the API before it lands.
// ---------------------------------------------------------------------------
var minioUser = builder.AddParameter("minio-root-user", "minioadmin");
var minioPassword = builder.AddParameter("minio-root-password", "minioadmin", secret: true);

var storage = builder.AddMinioContainer("storage", minioUser, minioPassword)
    .WithDataVolume();

// ---------------------------------------------------------------------------
// Web API — migrates the schema on start in Development, then serves.
// ---------------------------------------------------------------------------
var webapi = builder.AddProject<Projects.Kadari_WebApi>("webapi")
    .WithReference(kadariDb).WaitFor(kadariDb)
    .WithReference(storage).WaitFor(storage)
    .WithEnvironment("S3__ServiceUrl", storage.Resource.PrimaryEndpoint)
    .WithEnvironment("S3__AccessKey", minioUser)
    .WithEnvironment("S3__SecretKey", minioPassword)
    .WithEnvironment("S3__ForcePathStyle", "true")
    .WithEnvironment("S3__BucketName", "kadari");

// ---------------------------------------------------------------------------
// Customer app — the KADARI SPA. nginx reverse-proxies /api → the Web API (same origin, so the refresh cookie and
// phone demos over the LAN work). Fixed port 8200.
// ---------------------------------------------------------------------------
builder.AddDockerfile("client", "../Kadari.Client")
    .WithBuildArg("VITE_DEMO_TOOLS", "true")   // local demos only: the sample-household button
    .WithHttpEndpoint(port: 8200, targetPort: 8080, name: "http")
    .WithExternalHttpEndpoints()
    .WithReference(webapi)
    .WithEnvironment("API_URL", webapi.GetEndpoint("http"))
    .WaitFor(webapi);

// ---------------------------------------------------------------------------
// Portal — insurers (their portfolio, under their own branding) and KADARI staff (users and roles). Same nginx shape
// as the customer app. Fixed port 8300.
// ---------------------------------------------------------------------------
builder.AddDockerfile("portal", "../Kadari.Portal")
    .WithHttpEndpoint(port: 8300, targetPort: 8080, name: "http")
    .WithExternalHttpEndpoints()
    .WithReference(webapi)
    .WithEnvironment("API_URL", webapi.GetEndpoint("http"))
    .WaitFor(webapi);

builder.Build().Run();

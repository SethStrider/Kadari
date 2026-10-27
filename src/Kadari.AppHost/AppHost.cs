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
// Message broker — RabbitMQ. The API writes integration events to the outbox in its transaction and ships them here;
// the Worker is the only consumer (CLAUDE.md §6a). Management UI on the resource's management endpoint.
// ---------------------------------------------------------------------------
var messaging = builder.AddRabbitMQ("messaging")
    .WithDataVolume()
    .WithManagementPlugin();

// ---------------------------------------------------------------------------
// Migration runner — applies EF migrations and the idempotent seeds, then exits. The API and the Worker wait for it,
// exactly as a deploy runs `migrations` before `up`.
// ---------------------------------------------------------------------------
var migrations = builder.AddProject<Projects.Kadari_Migration>("migrations")
    .WithReference(kadariDb).WaitFor(kadariDb);

// ---------------------------------------------------------------------------
// Web API — serves once the schema is current.
// ---------------------------------------------------------------------------
// Local mail catcher. With no Twilio or SMTP configured, every SMS, WhatsApp and email the API would send lands in
// this inbox instead (sign-in codes included), at http://localhost:8400. Development only.
var mail = builder.AddMailPit("mailpit", httpPort: 8400);

var webapi = builder.AddProject<Projects.Kadari_WebApi>("webapi")
    .WithReference(mail).WaitFor(mail)
    .WithReference(kadariDb).WaitFor(kadariDb)
    .WithReference(messaging).WaitFor(messaging)
    .WaitForCompletion(migrations)
    .WithReference(storage).WaitFor(storage)
    .WithEnvironment("S3__ServiceUrl", storage.Resource.PrimaryEndpoint)
    .WithEnvironment("S3__AccessKey", minioUser)
    .WithEnvironment("S3__SecretKey", minioPassword)
    .WithEnvironment("S3__ForcePathStyle", "true")
    .WithEnvironment("S3__BucketName", "kadari");

// ---------------------------------------------------------------------------
// Worker — integration-event consumers and scheduled sweeps. Same database, storage and mail as the API.
// ---------------------------------------------------------------------------
builder.AddProject<Projects.Kadari_Worker>("worker")
    .WithReference(mail).WaitFor(mail)
    .WithReference(kadariDb).WaitFor(kadariDb)
    .WithReference(messaging).WaitFor(messaging)
    .WaitForCompletion(migrations)
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

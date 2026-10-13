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
builder.AddProject<Projects.Kadari_WebApi>("webapi")
    .WithReference(kadariDb).WaitFor(kadariDb)
    .WithReference(storage).WaitFor(storage)
    .WithEnvironment("S3__ServiceUrl", storage.Resource.PrimaryEndpoint)
    .WithEnvironment("S3__AccessKey", minioUser)
    .WithEnvironment("S3__SecretKey", minioPassword)
    .WithEnvironment("S3__ForcePathStyle", "true")
    .WithEnvironment("S3__BucketName", "kadari");

builder.Build().Run();

"""End-to-end smoke test of the standalone KADARI API: OTP sign-in, /me, ledger, consents."""
import hashlib, json, subprocess, time, urllib.error, urllib.request

API = "http://localhost:5235"
MOBILE = "+27821110001"


def call(method, path, body=None, token=None):
    req = urllib.request.Request(API + path, method=method,
                                 data=None if body is None else json.dumps(body).encode(),
                                 headers={"Content-Type": "application/json",
                                          **({"Authorization": "Bearer " + token} if token else {})})
    try:
        with urllib.request.urlopen(req, timeout=15) as r:
            raw = r.read().decode()
            return r.status, (json.loads(raw) if raw else None)
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode()[:300]


for _ in range(60):
    try:
        if urllib.request.urlopen(API + "/health", timeout=3).status == 200:
            break
    except Exception:
        time.sleep(5)
else:
    raise SystemExit("API never became healthy")

print("otp/request", call("POST", "/api/v1/auth/otp/request", {"mobileNumber": MOBILE})[0])

def holds_kadari(name):
    pw = subprocess.run(["docker", "exec", name, "printenv", "POSTGRES_PASSWORD"], capture_output=True, text=True).stdout.strip()
    out = subprocess.run(["docker", "exec", "-e", "PGPASSWORD=" + pw, name, "psql", "-U", "postgres", "-tAc",
                          "SELECT 1 FROM pg_database WHERE datname = 'kadaridb'"], capture_output=True, text=True).stdout
    return out.strip() == "1"


pg = next(n for n in subprocess.run(["docker", "ps", "--format", "{{.Names}}"], capture_output=True, text=True).stdout.split()
          if n.startswith("postgres-") and holds_kadari(n))
password = subprocess.run(["docker", "exec", pg, "printenv", "POSTGRES_PASSWORD"], capture_output=True, text=True).stdout.strip()
digest = subprocess.run(["docker", "exec", "-e", "PGPASSWORD=" + password, pg, "psql", "-U", "postgres", "-d", "kadaridb",
                         "-tAc", 'SELECT "CodeHash" FROM kadari."OtpChallenges" ORDER BY "CreatedAt" DESC LIMIT 1'],
                        capture_output=True, text=True).stdout.strip()
code = next(f"{i:06d}" for i in range(10 ** 6) if hashlib.sha256(f"{i:06d}".encode()).hexdigest() == digest.lower())

status, verified = call("POST", "/api/v1/auth/otp/verify", {"mobileNumber": MOBILE, "code": code})
print("otp/verify", status)
token = verified["accessToken"]

status, me = call("GET", "/api/v1/customers/me", token=token)
print("customers/me", status, me if status != 200 else {k: me[k] for k in list(me)[:3]})
customer = me["customerId"]

print("ledger", *call("GET", f"/api/v1/kadari/customers/{customer}/ledger", token=token))
print("consents", *call("GET", f"/api/v1/kadari/customers/{customer}/consents", token=token))
print("someone else's ledger", call("GET", "/api/v1/kadari/customers/00000000-0000-0000-0000-000000000001/ledger", token=token)[0])

# Capture: refused without consent, then a real JPEG becomes an asset in the ledger.
import io, uuid


def snap(jpeg):
    boundary = uuid.uuid4().hex
    detections = json.dumps([{"label": "tv", "confidence": 0.94, "x": 0, "y": 0, "width": 1, "height": 1}])
    parts = [
        f'--{boundary}\r\nContent-Disposition: form-data; name="image"; filename="snap.jpg"\r\nContent-Type: image/jpeg\r\n\r\n'.encode() + jpeg + b"\r\n",
        f'--{boundary}\r\nContent-Disposition: form-data; name="detections"\r\n\r\n{detections}\r\n'.encode(),
        f'--{boundary}\r\nContent-Disposition: form-data; name="roomLabel"\r\n\r\nLounge\r\n'.encode(),
        f"--{boundary}--\r\n".encode(),
    ]
    req = urllib.request.Request(f"{API}/api/v1/kadari/customers/{customer}/snaps", method="POST", data=b"".join(parts),
                                 headers={"Content-Type": f"multipart/form-data; boundary={boundary}", "Authorization": "Bearer " + token})
    try:
        with urllib.request.urlopen(req, timeout=60) as r:
            return r.status, json.loads(r.read().decode())
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode()[:200]


from PIL import Image
buffer = io.BytesIO()
Image.new("RGB", (64, 48), (120, 90, 60)).save(buffer, "JPEG")
jpeg = buffer.getvalue()

print("snap without consent", snap(jpeg)[0])
print("grant consent", call("POST", f"/api/v1/kadari/customers/{customer}/consents", {"scope": 0}, token=token)[0])
print("snap with consent", *snap(jpeg))
status, ledger = call("GET", f"/api/v1/kadari/customers/{customer}/ledger", token=token)
print("ledger after capture", status, {"assetCount": ledger["assetCount"], "rooms": [r.get("roomLabel") for r in ledger["rooms"]]})

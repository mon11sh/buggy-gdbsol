# 🛠️ Software Setup Guide — GDB Full-Stack Project

**Read me first.** This guide installs every tool you need for the GDB (Global
Digital Bank) project on **Windows, Linux, or macOS**. It is written so you can
follow it **on your own, start to finish**, even if this is your first time.

For each tool you will:
1. **Install** it (pick your operating system).
2. **Verify** it works (copy-paste one command — you should see a version number).

> 💡 **How to read this guide**
> - Lines in grey boxes are **commands** — type or paste them into your terminal and press **Enter**.
> - `$` or `>` at the start of a command line is just the prompt — **don't type it**.
> - After installing anything, **close and reopen your terminal** so it picks up the new tool.

---

## 📋 What you will install

| # | Software | Why the project needs it | Minimum version |
|---|----------|--------------------------|-----------------|
| 1 | **Git** | Download the code and track changes | Latest |
| 2 | **.NET SDK** | Runs the 8 backend microservices | **10.0.x** |
| 3 | **Node.js** | Runs the React frontend | **18+** (LTS) |
| 4 | **Docker Desktop** | (Optional) Run everything in containers | Latest |
| 5 | **PostgreSQL + pgAdmin** | Postgres database + its visual admin tool | 15 or 16 |
| 6 | **MySQL + Workbench** | MySQL database + its visual admin tool | 8.0 |
| 7 | **Supabase** | Cloud Postgres (online — no install) | Cloud account |

> You do **not** need all databases at once. The project runs on any **one** of:
> `inmemory` (zero setup), `sqlite` (zero setup), `postgres` (needs #5),
> `mysql` (needs #6), or `supabase` (needs #7). Beginners can start with
> **inmemory** and install databases later.

---

## 🧭 Recommended order & system requirements

**Install in this order:** Git → .NET SDK → Node.js → (Docker) → databases.

**Minimum laptop specs**
- **RAM:** 8 GB (16 GB recommended if you plan to use Docker).
- **Disk:** ~10 GB free.
- **OS:** Windows 10/11 (64-bit), macOS 12+, or Ubuntu 20.04+ / any modern Linux.
- **Internet:** required for downloads and for Supabase.

---

## ✅ Terminal cheat-sheet (open this first)

You will run commands in a **terminal**. Here is how to open one:

- **Windows:** Press `Windows key`, type **PowerShell**, click **Windows PowerShell**.
- **macOS:** Press `Cmd + Space`, type **Terminal**, press **Enter**.
- **Linux:** Press `Ctrl + Alt + T` (Ubuntu) or search **Terminal**.

**Package managers make installs easier (optional but recommended):**
- **Windows** has **winget** built in (Windows 10/11). Verify: `winget --version`
- **macOS** — install **Homebrew** once (see the box below).
- **Linux (Ubuntu/Debian)** has **apt** built in. Fedora uses **dnf**.

> 🍺 **macOS: install Homebrew first (one time).** Paste this into Terminal and follow the prompts:
> ```bash
> /bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
> ```
> After it finishes, run the two `echo ... >> ~/.zprofile` / `eval` lines it prints (Apple-Silicon Macs) so `brew` is on your PATH. Verify: `brew --version`

---

# 1️⃣ Git

Git downloads the project code and lets you track your changes.

### Windows
**Option A — winget (fastest):**
```powershell
winget install --id Git.Git -e
```
**Option B — installer:**
1. Go to **https://git-scm.com/download/win** (download starts automatically).
2. Run the `.exe`. Click **Next** through every screen (the defaults are correct).
3. On "Choosing the default editor" pick **Notepad** or **VS Code** if unsure.
4. Finish the install.

### macOS
```bash
brew install git
```
*(Alternatively, running `git --version` once will offer to install Apple's "Command Line Tools".)*

### Linux
**Ubuntu / Debian:**
```bash
sudo apt update && sudo apt install -y git
```
**Fedora:**
```bash
sudo dnf install -y git
```

### ✅ Verify (all OSes)
```bash
git --version
```
You should see something like `git version 2.44.0`.

### 🔧 First-time Git setup (do this once)
```bash
git config --global user.name "Your Name"
git config --global user.email "you@example.com"
```

---

# 2️⃣ .NET SDK (10.0.x)

The .NET SDK compiles and runs all 8 backend services. **Version matters** — install 10.0.x.

### Windows
**Option A — winget:**
```powershell
winget install Microsoft.DotNet.SDK.10
```
**Option B — installer:**
1. Go to **https://dotnet.microsoft.com/download/dotnet/10.0**.
2. Download **"Windows x64"** installer.
3. Run the installer and complete the setup.

### macOS
```bash
brew install --cask dotnet-sdk
```

### Linux
**Ubuntu / Debian:**
```bash
sudo apt-get update && \
  sudo apt-get install -y dotnet-sdk-10.0
```

### ✅ Verify
```bash
dotnet --version
```
You should see `10.0.x`.


---

# 3️⃣ Node.js (18+ LTS)

Node.js runs the React frontend (the website you see in the browser).

### Windows
**Option A — winget:**
```powershell
winget install --id OpenJS.NodeJS.LTS -e
```
**Option B — installer:**
1. Go to **https://nodejs.org/en/download** and download the **LTS** Windows `.msi`.
2. Run it, click **Next** through the defaults, and **Install**.

### macOS
```bash
brew install node@20
```

### Linux (Ubuntu / Debian) — use NodeSource for a current LTS
```bash
curl -fsSL https://deb.nodesource.com/setup_20.x | sudo -E bash -
sudo apt install -y nodejs
```
**Fedora:**
```bash
sudo dnf install -y nodejs
```

### ✅ Verify
```bash
node --version     # expect v18.x or v20.x
npm --version      # npm comes bundled with Node
```

---

# 4️⃣ Docker Desktop (optional — for the container-based setup)

Docker lets you run the **entire** project (all services + databases) with **one
command**, without installing Python/Node/databases separately. Install it only
if your laptop has **16 GB RAM** and you prefer the container route.

> If you are **not** using Docker, **skip to section 5**.

### Windows
1. **Enable virtualization** (usually already on): Docker uses **WSL 2**. Open
   PowerShell **as Administrator** and run:
   ```powershell
   wsl --install
   ```
   Restart your laptop if it asks you to.
2. Download **Docker Desktop** from **https://www.docker.com/products/docker-desktop/**.
3. Run the installer, keep **"Use WSL 2 instead of Hyper-V"** ticked, finish, and **restart**.
4. Launch **Docker Desktop** and wait until the whale icon in the tray says **"Docker Desktop is running"**.

### macOS
```bash
brew install --cask docker
```
Then open **Docker Desktop** from Applications once so it can finish setup (grant
permissions when asked). Choose the **Apple chip** or **Intel chip** build if you
download manually from the website.

### Linux (Docker Engine + Compose plugin — Ubuntu/Debian)
```bash
# Official convenience script (installs Docker Engine + Compose):
curl -fsSL https://get.docker.com | sudo sh
# Run docker without sudo (log out & back in afterwards):
sudo usermod -aG docker $USER
```

### ✅ Verify
```bash
docker --version
docker compose version
docker run hello-world     # downloads a tiny test image and prints a success message
```
> The `hello-world` run must print **"Hello from Docker!"**. On Windows/macOS,
> **Docker Desktop must be open/running** for any `docker` command to work.

---

# 5️⃣ PostgreSQL + pgAdmin

**PostgreSQL** is the database engine; **pgAdmin** is the visual tool to browse it.
The official PostgreSQL installer includes **both**.

### Windows
1. Go to **https://www.postgresql.org/download/windows/** → **"Download the installer"** (EDB).
2. Choose **PostgreSQL 16** (or 15) and run the installer.
3. Click **Next** through the screens. When asked:
   - **Installation Directory:** keep the default.
   - **Select Components:** keep **PostgreSQL Server**, **pgAdmin 4**, and **Command Line Tools** ticked.
   - **Password:** set a password for the **`postgres`** superuser — **write it down!** (e.g. `postgres`).
   - **Port:** keep **5432**.
   - **Locale:** keep the default.
4. Finish. You can skip "Stack Builder" at the end (click **Cancel**).

### macOS
**Option A — Postgres.app (easiest, includes a menu-bar server):**
1. Download from **https://postgresapp.com/**, drag to **Applications**, open it, click **Initialize**.

**Option B — Homebrew:**
```bash
brew install postgresql@16
brew services start postgresql@16     # starts the server now and on login
```
**pgAdmin on macOS** (visual tool):
```bash
brew install --cask pgadmin4
```

### Linux (Ubuntu / Debian)
```bash
sudo apt update
sudo apt install -y postgresql postgresql-contrib
sudo systemctl enable --now postgresql        # start on boot + now
```
**pgAdmin (desktop) on Ubuntu:**
```bash
curl -fsS https://www.pgadmin.org/static/packages_pgadmin_org.pub | sudo gpg --dearmor -o /usr/share/keyrings/packages-pgadmin-org.gpg
sudo sh -c 'echo "deb [signed-by=/usr/share/keyrings/packages-pgadmin-org.gpg] https://ftp.postgresql.org/pub/pgadmin/pgadmin4/apt/$(lsb_release -cs) pgadmin4 main" > /etc/apt/sources.list.d/pgadmin4.list'
sudo apt update && sudo apt install -y pgadmin4-desktop
```
Set the Postgres `postgres` user's password (Linux) so the project can connect:
```bash
sudo -u postgres psql -c "ALTER USER postgres PASSWORD 'postgres';"
```

### ✅ Verify PostgreSQL
```bash
psql --version                 # shows the client version
# Connect (enter the postgres password you set):
psql -U postgres -h localhost  # type \q then Enter to quit
```

### ✅ Open pgAdmin and connect to your server
1. Launch **pgAdmin 4** (Start Menu / Applications / app menu).
2. The first time, it asks you to set a **master password** for pgAdmin itself — set one.
3. In the left tree, expand **Servers**. If nothing is there, right-click **Servers → Register → Server…**:
   - **General tab → Name:** `Local` (any name).
   - **Connection tab → Host:** `localhost`, **Port:** `5432`, **Username:** `postgres`,
     **Password:** the one you set during install. Tick **Save password**. Click **Save**.
4. You should now see your databases under the server. 🎉

> The project auto-creates its own databases (`gdb_accounts_db`, `gdb_auth_db`,
> `gdb_users_db`) on first run — you do **not** need to create them by hand.

---

# 6️⃣ MySQL + MySQL Workbench

**MySQL** is the database engine; **MySQL Workbench** is the visual tool.

### Windows
1. Go to **https://dev.mysql.com/downloads/installer/** → download **"MySQL Installer for Windows"** (the larger, offline `mysql-installer-community` file).
2. Run it. Choose **Setup Type: "Developer Default"** (installs Server + Workbench + connectors) or **"Custom"** and pick **MySQL Server** + **MySQL Workbench**.
3. Click **Execute** to download/install the selected products.
4. In **"Type and Networking"** keep **Port 3306**.
5. In **"Authentication Method"** choose **"Use Strong Password Encryption"**.
6. Set a **root password** — **write it down!** (e.g. `mysql`).
7. Keep the Windows Service enabled (starts MySQL automatically). Finish.

### macOS
```bash
brew install mysql
brew services start mysql          # start the server now and on login
mysql_secure_installation          # set the root password when prompted
```
**MySQL Workbench:**
```bash
brew install --cask mysqlworkbench
```

### Linux (Ubuntu / Debian)
```bash
sudo apt update
sudo apt install -y mysql-server
sudo systemctl enable --now mysql
sudo mysql_secure_installation     # set root password + answer the prompts
```
**MySQL Workbench:**
```bash
sudo apt install -y mysql-workbench
```
> If the project connects as `root`, set a password it can use:
> ```bash
> sudo mysql -e "ALTER USER 'root'@'localhost' IDENTIFIED WITH mysql_native_password BY 'mysql'; FLUSH PRIVILEGES;"
> ```

### ✅ Verify MySQL
```bash
mysql --version
# Connect (enter your root password):
mysql -u root -p        # type  exit  then Enter to quit
```

### ✅ Open MySQL Workbench and connect
1. Launch **MySQL Workbench**.
2. Click the **➕** next to "MySQL Connections" (or use the default **Local instance** tile):
   - **Connection Name:** `Local`, **Hostname:** `127.0.0.1`, **Port:** `3306`, **Username:** `root`.
   - Click **Test Connection**, enter your root password. A green success message = ✅.
3. Double-click the connection to open it and browse your schemas.

---

# 7️⃣ Supabase (cloud Postgres — nothing to install)

Supabase gives you a **free Postgres database in the cloud**. There is **no
software to install** — you create an online account and copy the connection
details into the project.

### Step-by-step
1. Go to **https://supabase.com/** and click **Start your project** / **Sign in**.
   Sign up (GitHub login is easiest).
2. Click **New project**:
   - **Organization:** pick or create one.
   - **Name:** e.g. `gdb-training`.
   - **Database Password:** click **Generate** (or type your own) — **copy and save this password!**
   - **Region:** pick the one closest to you.
   - Click **Create new project** and wait ~1–2 minutes while it provisions.
3. Get your connection details: open your project → **⚙️ Project Settings** (bottom-left)
   → **Database** → find the **Connection info** / **Connection string** section. Note:
   - **Host** (e.g. `db.xxxxxxxx.supabase.co`)
   - **Port** (`5432`, or `6543` for the pooled connection)
   - **User** (`postgres`)
   - **Password** (the one you saved in step 2)
   - **Database name** (`postgres`)
4. Put these into the project's environment file. From the project root, copy the
   example and fill in your values:
   ```bash
   # from the gdb-service folder:
   cp supabase.env.example .env      # macOS / Linux
   copy supabase.env.example .env    # Windows (PowerShell: Copy-Item supabase.env.example .env)
   ```
   Open `.env` in an editor and set the Supabase host/user/password/db values you copied.

### ✅ Verify Supabase
- In the Supabase dashboard, open **Table Editor** or **SQL Editor** — if it loads, your project is live.
- (Optional) test the connection from your laptop with `psql`:
  ```bash
  psql "postgresql://postgres:YOUR_PASSWORD@YOUR_HOST:5432/postgres"
  ```
  A `postgres=>` prompt means it works. Type `\q` to quit.

> 🔒 **Never commit your `.env` or real Supabase password to Git.** The project's
> `.gitignore` already excludes `.env`.

---

# 🧪 Final verification checklist

Open a **fresh terminal** and run these. Every line should print a version (Docker
and the databases only if you installed them):

```bash
git --version
dotnet --version
node --version
npm --version
docker --version         # only if you installed Docker
docker compose version   # only if you installed Docker
psql --version           # only if you installed PostgreSQL
mysql --version          # only if you installed MySQL
```

| Tool | Command | Looks like |
|------|---------|-----------|
| Git | `git --version` | `git version 2.44.0` |
| .NET | `dotnet --version` | `10.0.x` |
| Node | `node --version` | `v20.11.1` |
| npm | `npm --version` | `10.5.0` |
| Docker | `docker --version` | `Docker version 26.x` |
| Compose | `docker compose version` | `Docker Compose version v2.x` |
| PostgreSQL | `psql --version` | `psql (PostgreSQL) 16.x` |
| MySQL | `mysql --version` | `mysql  Ver 8.0.x` |
| Supabase | dashboard loads | project shows **Active** |

✅ If all your required tools print a version, **you are ready.** Next, follow the
project's run guide (local: `dotnet run --project tools/Gdb.Setup` → `dotnet run --project tools/Gdb.Runner`; Docker: `dotnet run --project tools/Gdb.DockerUp <provider>`).

---

# 🆘 Troubleshooting (common issues)

**"`dotnet` / `node` / `git` is not recognized" (command not found)**
- You didn't reopen the terminal after installing — **close and reopen it**.


**Docker command fails / "Cannot connect to the Docker daemon"**
- **Open Docker Desktop** and wait for the whale icon to say **"running"** (Windows/macOS).
- Linux: run `sudo systemctl start docker`, and if you get "permission denied", finish `sudo usermod -aG docker $USER` then **log out and back in**.

**Port already in use (5432 / 3306 / 8001…)**
- Another copy of the database (or a previous run) is using the port. Stop the old
  one, or change the port. On Windows, an existing PostgreSQL/MySQL service may
  already own 5432/3306 — that's fine, use it.

**pgAdmin/Workbench won't connect**
- Double-check **host** (`localhost` / `127.0.0.1`), **port**, **username**, and the
  **password you set during install**. Make sure the database **service is running**
  (Windows: *Services* app; macOS: `brew services list`; Linux: `systemctl status postgresql`/`mysql`).

**macOS: `brew: command not found`**
- Homebrew isn't on your PATH yet — re-run the two lines Homebrew printed at the end
  of its install (the `echo ... >> ~/.zprofile` and `eval ...` lines), then reopen Terminal.

**Linux: `psql`/`mysql` client installed but "connection refused"**
- The **server** isn't running. Start it: `sudo systemctl start postgresql` or `sudo systemctl start mysql`.

---

## 📎 Quick download links

| Software | Link |
|----------|------|
| Git | https://git-scm.com/downloads |
| .NET SDK | https://dotnet.microsoft.com/download |
| Node.js (LTS) | https://nodejs.org/en/download |
| Docker Desktop | https://www.docker.com/products/docker-desktop/ |
| PostgreSQL + pgAdmin | https://www.postgresql.org/download/ |
| pgAdmin (standalone) | https://www.pgadmin.org/download/ |
| MySQL Installer | https://dev.mysql.com/downloads/installer/ |
| MySQL Workbench | https://dev.mysql.com/downloads/workbench/ |
| Supabase | https://supabase.com/ |

---

*You've installed the toolbox. Head to the project's run guide next — start with the
**inmemory** provider (zero database setup) to see the app working, then switch to
Postgres/MySQL/Supabase once you're comfortable.*



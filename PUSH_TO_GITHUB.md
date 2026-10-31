# How to push this project to GitHub

The repository already contains an initial commit on the `main` branch — you only need to create the remote repo and push.

## 1. Create an empty repository on GitHub

1. Go to <https://github.com/new>
2. Repository name: **KamCo**
3. Keep it **empty** — do *not* add README, .gitignore or LICENSE (they already exist here)
4. Click **Create repository**

## 2. Push

```bash
cd KamCo

# point the local repo at your new GitHub repo
git remote add origin https://github.com/<YOUR-USERNAME>/KamCo.git

# push the existing commit
git push -u origin main
```

GitHub will ask for credentials. Use **your username + a Personal Access Token (PAT)** — not your password.
Create a PAT under: *Settings → Developer settings → Personal access tokens → Tokens (classic) → Generate new token* with the `repo` scope.

## 3. Verify

Refresh <https://github.com/<YOUR-USERNAME>/KamCo> — you should see the source code, README rendered and the commit.

## Troubleshooting

- **`remote origin already exists`** → run `git remote set-url origin https://github.com/<YOUR-USERNAME>/KamCo.git`
- **Wrong branch name** (`master` instead of `main`) → `git branch -M main && git push -u origin main`
- **Auth failed** → make sure you use the PAT as the password, or switch to SSH:
  `git remote set-url origin git@github.com:<YOUR-USERNAME>/KamCo.git`
- **Update the commit author** (optional):
  `git config user.name "Your Name" && git config user.email "you@example.com"` then
  `git commit --amend --reset-author --no-edit` and push again.

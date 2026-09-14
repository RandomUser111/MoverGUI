# Публикация Mover GUI на GitHub через PowerShell

## 1. Откройте PowerShell от обычного пользователя

Перейдите в каталог проекта:

```powershell
cd "C:\Dev\MoverGUI"
```

## 2. Установите Git и GitHub CLI

```powershell
winget install --id Git.Git -e --source winget
winget install --id GitHub.cli -e --source winget
```

Закройте и снова откройте PowerShell.

Проверьте:

```powershell
git --version
gh --version
```

## 3. Один раз настройте Git

```powershell
git config --global user.name "YOUR NAME"
git config --global user.email "YOUR_GITHUB_EMAIL"
```

## 4. Войдите в GitHub CLI

```powershell
gh auth login
```

Рекомендуемый выбор:

- GitHub.com
- HTTPS
- Login with a web browser

Проверка:

```powershell
gh auth status
```

## 5. Создайте локальный Git-репозиторий

```powershell
git init
git branch -M main
git add .
git status
git commit -m "Initial release of Mover GUI"
```

## 6. Создайте репозиторий на GitHub и отправьте код

Публичный репозиторий:

```powershell
gh repo create MoverGUI --public --source=. --remote=origin --push --description "Compact Windows GUI utility for copying files to remote SetRetail cash registers over SSH/SCP"
```

Для приватного репозитория замените `--public` на `--private`.

## 7. Откройте репозиторий

```powershell
gh repo view --web
```

## 8. Создайте первый GitHub Release

Сначала убедитесь, что все изменения закоммичены:

```powershell
git status
```

Затем:

```powershell
.\Create_Release.ps1 -Version 1.0.0 -Publish
```

Скрипт:

1. собирает self-contained `win-x64` EXE;
2. создаёт ZIP;
3. считает SHA256;
4. создаёт git-тег `v1.0.0`;
5. отправляет тег на GitHub;
6. создаёт GitHub Release;
7. прикладывает EXE, ZIP и `SHA256SUMS.txt`.

Если нужен только локальный комплект бинарников:

```powershell
.\Create_Release.ps1 -Version 1.0.0
```

# DbOps

คำอธิบาย

โครงการนี้เป็นแอปคอนโซล .NET 10 สำหรับงานจัดการฐานข้อมูล (DB operations) โดยใช้ `Microsoft.SqlServer.DacFx` และการตั้งค่าจาก `appsettings.json`.

ความต้องการ (Prerequisites)

- .NET 10 SDK
- PowerShell หรือ shell ที่ต้องการ

เริ่มต้น (Setup)

1. Restore และ build:

   ```powershell
   dotnet restore
   dotnet build
   ```

2. รันโปรเจค:

   ```powershell
   dotnet run --project DbOps
   ```

การตั้งค่า (Configuration)

ไฟล์ `appsettings.json` จะถูกคัดลอกไปยัง output; แก้ connection string หรือค่าต่าง ๆ ในไฟล์นั้นตามต้องการ

การร่วมพัฒนา (Contributing)

- ใช้รูปแบบ commit ตาม Conventional Commits (ตัวอย่างใน template commit)
- สร้าง branch ใหม่สำหรับฟีเจอร์หรือบั๊กที่จะแก้

ตั้งค่า template commit (ตัวเลือก):

```powershell
git config --local commit.template .github/COMMIT_TEMPLATE.md
```

ลิขสิทธิ์

MIT

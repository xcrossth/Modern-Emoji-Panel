# Automated tests

โฟลเดอร์นี้เป็นรากของ test projects ที่ใช้ร่วมกันใน monorepo ส่วน regression gate ของ foundation ปัจจุบันอยู่ที่ `scripts/verify-foundation.ps1` และ `scripts/test-clean-checkout.ps1`

เมื่อเพิ่ม test project ให้เพิ่มเข้า `ModernEmojiPanel.sln` และเก็บ NuGet lock file ของ project นั้นใน Git

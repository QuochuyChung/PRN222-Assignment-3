# Seed — Interview Engine (ENG-10)

Câu trả lời mẫu để thử luồng hỏi xoáy **không cần mic thật**. Mỗi loại tương ứng một nhánh
của `FollowUpDecider` + luật cứng (ENG-05). Dùng để dựng kịch bản demo và cho `FakeLlmRepository`
(QST-02) khi được Dev B thêm.

Câu hỏi tham chiếu: **"Razor Pages khác MVC ở điểm nào?"**
Ý chính: `PageModel`, `routing theo thư mục`, `phù hợp ứng dụng nhỏ`.

## 1. Đủ ý → không hỏi xoáy, sang câu tiếp theo

```
Razor Pages thì mỗi trang có một PageModel riêng, routing dựa theo thư mục của trang
nên không cần khai báo route. Nó phù hợp với ứng dụng nhỏ, ít logic dùng chung.
```

Kỳ vọng: `needFollowUp=false`, `missingKeyPoints=[]`, state → `NextQuestion`.

## 2. Thiếu ý → hỏi xoáy

```
Razor Pages thì mỗi trang có một PageModel riêng thầy ạ.
```

Kỳ vọng: `needFollowUp=true`, `missingKeyPoints` gồm `routing theo thư mục` và
`phù hợp ứng dụng nhỏ`, câu hỏi xoáy kiểu "Khi nào bạn chọn Razor Pages thay vì MVC?".
Sau `MaxFollowUps` lượt, server ép sang câu mới dù LLM vẫn muốn hỏi tiếp.

## 3. Mơ hồ → hỏi xoáy làm rõ

```
Cái đó thì cũng na ná nhau thôi thầy, em học rồi nhưng không nhớ rõ.
```

Kỳ vọng: `needFollowUp=true`, `coveredKeyPoints=[]`, `missingKeyPoints` đủ ba ý.

## 4. Im lặng → nhắc một lần rồi bỏ qua (không gọi LLM)

Transcript rỗng (`""` hoặc toàn khoảng trắng). Lượt 1 trả lại đúng câu hỏi (nhắc),
lượt 2 publish một `TurnMessage` với `transcript=""` và `reason` nói rõ sinh viên im lặng,
state sang câu kế tiếp.

## 5. Quá giờ → server bỏ qua kết luận LLM

Trả lời sau `deadline`. Dù `needFollowUp=true`, state luôn → `NextQuestion`
(`remainingSeconds` do server tính, không tin client).

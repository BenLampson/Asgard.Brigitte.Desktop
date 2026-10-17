using System.Security.Cryptography;
namespace Asgard.Brigitte.Desktop.Core;
/// <summary>原生登录的本地回调页；不向页面输出授权码、令牌或错误原文。</summary>
public sealed class LoginCallbackPage
{
    /// <summary>每次页面独立 CSP nonce，只允许产品内置脚本与样式。</summary>
    public string Nonce { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
    /// <summary>浏览器先显示校验中；徽记来自应用的同一份内置 SVG。</summary>
    public string Render(string logoSvg) => $$$"""
        <!doctype html><html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
        <title>Brigitte · 正在完成登录</title><link rel="icon" href="data:image/svg+xml;base64,{{{Convert.ToBase64String(Encoding.UTF8.GetBytes(logoSvg))}}}"><style nonce="{{{Nonce}}}">
        :root{color-scheme:light dark;font-family:"Segoe UI","Microsoft YaHei",system-ui,sans-serif;color:#1d3445;background:#f7f5ef}
        *{box-sizing:border-box}body{margin:0;min-height:100vh;display:grid;place-items:center;padding:24px}
        main{width:min(100%,460px);padding:44px 40px;background:#fffdf8;border:1px solid #e2dfd6;border-radius:20px;text-align:center}
        img{width:64px;height:64px;display:block;margin:0 auto 20px}.brand{font-size:15px;font-weight:600;letter-spacing:.02em;margin:0 0 28px}
        h1{font-size:24px;line-height:1.4;margin:0 0 12px}p{color:#58636e;font-size:14px;line-height:1.8;margin:0}
        #status{display:inline-flex;align-items:center;gap:7px;font-size:12px;color:#9c5e34;background:#f3e9dc;padding:5px 10px;border-radius:20px;margin-bottom:16px}
        #status::before{content:"";width:6px;height:6px;border-radius:50%;background:currentColor}
        button{font:inherit;font-size:14px;background:#173346;color:white;border:0;border-radius:8px;padding:11px 22px;margin:26px 0 14px;cursor:pointer}
        button:hover{background:#284959}button:focus-visible{outline:3px solid #d7a36c;outline-offset:3px}small{display:block;font-size:12px;color:#6b7680;line-height:1.7}
        body[data-result="success"] #status{color:#18734c;background:#e8f5ee}body[data-result="failure"] #status{color:#a43a30;background:#fff0ed}
        @media(prefers-color-scheme:dark){:root{color:#eef3f8;background:#142b40}main{background:#1b2c38;border-color:#354650}p,small{color:#b6c1cc}#status{color:#edc494;background:#263b48}body[data-result="success"] #status{color:#86d7b0;background:#203e32}body[data-result="failure"] #status{color:#ffb4aa;background:#4b2b29}}
        @media(max-width:480px){main{padding:32px 24px}}
        </style></head><body data-result="pending"><main>
        <img alt="Brigitte" src="data:image/svg+xml;base64,{{{Convert.ToBase64String(Encoding.UTF8.GetBytes(logoSvg))}}}">
        <div class="brand">ASGARD · BRIGITTE</div><div id="status" role="status" aria-live="polite">正在验证</div>
        <h1 id="heading">正在完成登录</h1><p id="message">授权信息已送达，客户端正在验证登录结果。</p>
        <button id="close" type="button">关闭此页面</button><small>您可以关闭此页面，回到 Brigitte 继续使用。<br>若浏览器未自动关闭，请直接关闭当前标签页。</small>
        </main><script nonce="{{{Nonce}}}">history.replaceState(null,'','/callback');document.getElementById('close').addEventListener('click',()=>window.close());</script>
        """;
    /// <summary>流式补充最终状态，成功提示不早于客户端验证和提交。</summary>
    public string Complete(bool success) => $$$"""
        <script nonce="{{{Nonce}}}">document.body.dataset.result='{{{(success ? "success" : "failure")}}}';document.title='Brigitte · {{{(success ? "登录成功" : "登录未完成")}}}';document.getElementById('status').textContent='{{{(success ? "验证完成" : "请返回客户端")}}}';document.getElementById('heading').textContent='{{{(success ? "登录成功" : "登录未完成")}}}';document.getElementById('message').textContent='{{{(success ? "已返回 Brigitte，您可以关闭此页面。" : "请返回 Brigitte 查看错误信息后重试，此页面可以关闭。")}}}';</script></body></html>
        """;
}

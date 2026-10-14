import { useEffect, useState } from "react";
import QRCode from "qrcode";
import { Check, Copy } from "lucide-react";

import { assetPassportVerifyUrl } from "@/lib/api/kadari";

/** QR + copyable link to the passport's public verify page. Always dark-on-white: scanners need the contrast. */
export function PassportQr({ verificationId }: { verificationId: string }) {
  const url = assetPassportVerifyUrl(verificationId);
  const [svg, setSvg] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    let live = true;
    QRCode.toString(url, {
      type: "svg",
      margin: 1,
      errorCorrectionLevel: "M",
      color: { dark: "#001920", light: "#FFFFFF" },
    })
      .then((markup) => live && setSvg(markup))
      .catch(() => live && setSvg(null));
    return () => {
      live = false;
    };
  }, [url]);

  async function copy() {
    try {
      await navigator.clipboard.writeText(url);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Clipboard can be blocked (insecure context / permissions) — the link stays visible to copy by hand.
    }
  }

  return (
    <div className="flex items-center gap-(--space-5)">
      <div
        role="img"
        aria-label={`QR code linking to the verification page for ${verificationId}`}
        className="size-28 shrink-0 overflow-hidden rounded-(--radius-sm) border border-(--color-border) bg-white p-(--space-1) [&_svg]:size-full"
        // Library-generated SVG from our own URL — no user markup.
        dangerouslySetInnerHTML={svg ? { __html: svg } : undefined}
      />
      <div className="flex min-w-0 flex-col gap-(--space-3)">
        <p className="text-sm text-(--color-text-muted)">
          Anyone can scan this to check the passport is genuine and current — no
          account needed. Your name is never shown.
        </p>
        <button
          type="button"
          onClick={() => void copy()}
          className="kd-focus inline-flex items-center gap-(--space-2) self-start text-sm font-medium text-(--color-text-link) hover:underline"
        >
          {copied ? (
            <Check className="size-4" aria-hidden />
          ) : (
            <Copy className="size-4" aria-hidden />
          )}
          {copied ? "Link copied" : "Copy verification link"}
        </button>
      </div>
    </div>
  );
}

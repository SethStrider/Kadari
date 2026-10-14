import { Toaster as Sonner, type ToasterProps } from "sonner";

/** Sonner toaster pre-themed for Trustli's dark surfaces. */
export function Toaster(props: ToasterProps) {
  return (
    <Sonner
      theme="dark"
      position="bottom-right"
      toastOptions={{
        classNames: {
          toast:
            "!bg-card !border !border-slate !text-off-white !rounded-2xl !font-sans",
          description: "!text-muted-foreground",
          actionButton: "!bg-gradient-brand !text-white !rounded-full",
        },
      }}
      {...props}
    />
  );
}

import * as React from "react";
import { Slot } from "@radix-ui/react-slot";
import { cva, type VariantProps } from "class-variance-authority";

import { cn } from "@/lib/utils";

/**
 * Trustli button variants.
 *  - primary  : pill-shaped, Cyan→Blue gradient, white text
 *  - secondary: dark navy with a cyan/slate border, pill-shaped
 *  - ghost    : text only, cyan on hover
 */
const buttonVariants = cva(
  "inline-flex items-center justify-center gap-2 whitespace-nowrap font-heading font-semibold transition-all duration-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-background disabled:pointer-events-none disabled:opacity-50 [&_svg]:size-4 [&_svg]:shrink-0",
  {
    variants: {
      variant: {
        primary:
          "rounded-full text-white bg-gradient-brand shadow-[0_4px_20px_-6px_rgba(9,91,255,0.6)] hover:brightness-110 hover:shadow-[0_6px_28px_-6px_rgba(0,220,194,0.7)] active:brightness-95",
        secondary:
          "rounded-full bg-trustli-navy text-off-white border border-slate hover:border-trustli-cyan hover:text-trustli-cyan",
        ghost:
          "rounded-full text-soft-grey hover:text-trustli-cyan hover:bg-white/5",
        destructive:
          "rounded-full bg-destructive text-destructive-foreground hover:brightness-110",
      },
      size: {
        sm: "h-9 px-4 text-sm",
        default: "h-11 px-6 text-sm",
        lg: "h-13 px-8 text-base",
        icon: "size-11 rounded-full",
      },
    },
    defaultVariants: {
      variant: "primary",
      size: "default",
    },
  },
);

export interface ButtonProps
  extends
    React.ButtonHTMLAttributes<HTMLButtonElement>,
    VariantProps<typeof buttonVariants> {
  asChild?: boolean;
}

const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, variant, size, asChild = false, ...props }, ref) => {
    const Comp = asChild ? Slot : "button";
    return (
      <Comp
        ref={ref}
        className={cn(buttonVariants({ variant, size, className }))}
        {...props}
      />
    );
  },
);
Button.displayName = "Button";

export { Button, buttonVariants };

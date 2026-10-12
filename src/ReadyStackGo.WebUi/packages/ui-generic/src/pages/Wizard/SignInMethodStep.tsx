import { useState } from 'react';
import type { IdentityProviderTemplateDto } from '@rsgo/core';
import { Button } from '../../components/ui/Button';
import { ProviderMark, SignInOptionCard } from '../../components/sso/SsoComponents';
import { useRadioKeys } from '../../hooks/useRadioKeys';
import { BUILT_IN, canContinueFromMethod } from './wizardFlow';

// First wizard step "How do you want to sign in?" (design frames 171:2417, 171:2534).

interface SignInMethodStepProps {
  templates: IdentityProviderTemplateDto[];
  initialSelection?: string | null;
  onContinue: (selection: string) => void;
}

export default function SignInMethodStep({ templates, initialSelection = null, onContinue }: SignInMethodStepProps) {
  const [selection, setSelection] = useState<string | null>(initialSelection);
  const options = [
    {
      id: BUILT_IN,
      title: 'Built-in sign-in',
      description: 'Username and password, stored in ReadyStackGo. Works without internet access.',
      mark: <ProviderMark kind="builtIn" />,
    },
    ...templates.map((t) => ({
      id: t.id,
      title: t.name,
      description: t.setupDescription || t.description,
      mark: <ProviderMark iconUrl={t.iconUrl} />,
    })),
  ];
  const keys = useRadioKeys(options.length, (i) => setSelection(options[i].id));
  const selectedIndex = Math.max(0, options.findIndex((o) => o.id === selection));

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h2 id="wizard-method" className="mb-1.5 text-xl font-semibold text-fg">
          How do you want to sign in?
        </h2>
        <p className="text-sm text-fg-secondary">
          Choose how the first administrator signs in. You can add more sign-in providers later under Settings › Single
          Sign-On.
        </p>
      </div>

      <div role="radiogroup" aria-labelledby="wizard-method" className="grid gap-4 sm:grid-cols-2">
        {options.map((o, i) => (
          <SignInOptionCard
            key={o.id}
            title={o.title}
            description={o.description}
            mark={o.mark}
            selected={o.id === selection}
            onSelect={() => setSelection(o.id)}
            tabIndex={i === selectedIndex ? 0 : -1}
            onKeyDown={(e) => keys.onKeyDown(e, i)}
            buttonRef={keys.setRef(i)}
            testId={`sign-in-option-${o.id}`}
          />
        ))}
      </div>

      <Button
        className="w-full"
        disabled={!canContinueFromMethod(selection)}
        onClick={() => selection && onContinue(selection)}
      >
        Continue
      </Button>
    </div>
  );
}

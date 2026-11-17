import { useCallback, useEffect, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { getOidcTemplates, setupApi, type IdentityProviderTemplateDto, type SetupSessionDto } from "@rsgo/core";
import { Alert } from "../../../components/ui/Alert";
import { Stepper } from "../../../components/ui/Stepper";
import { ProviderMark, SignInOptionCard } from "../../../components/sso/SsoComponents";
import { useRadioKeys } from "../../../hooks/useRadioKeys";
import { SettingsBreadcrumb } from "./oidcShared";
import { initialStep, setupSteps, stepSourceOf, type SetupStepId } from "./setupSteps";
import {
  ConnectStep,
  InstallationStep,
  ProviderAddressStep,
  RegisterStep,
  SaveStep,
  StepFooter,
  StepHeading,
  TestStep,
} from "./steps/SetupSteps";

// Settings › Single Sign-On › Add provider (design frames 173:3277 … 176:4615).

export default function AddOidcProviderPage() {
  const navigate = useNavigate();
  const [params, setParams] = useSearchParams();
  const sessionId = params.get("session");

  const [templates, setTemplates] = useState<IdentityProviderTemplateDto[]>([]);
  const [selected, setSelected] = useState<string | null>(null);
  const [session, setSession] = useState<SetupSessionDto | null>(null);
  const [step, setStep] = useState<SetupStepId>("template");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    getOidcTemplates()
      .then((list) => {
        setTemplates(list);
        setSelected((current) => current ?? list[0]?.id ?? null);
      })
      .catch((err) => setError(err instanceof Error ? err.message : "Failed to load templates"));
  }, []);

  // Open an existing session (also after a return from the provider).
  useEffect(() => {
    if (!sessionId || session?.id === sessionId) return;
    setupApi
      .get(sessionId)
      .then((s) => {
        setSession(s);
        setSelected(s.templateId);
        setStep(initialStep(s));
      })
      .catch(() => {
        setError("This setup has expired. Start again.");
        setParams({}, { replace: true });
      });
  }, [sessionId, session?.id, setParams]);

  const templateKeys = useRadioKeys(templates.length, (i) => setSelected(templates[i].id));
  const selectedTemplate = templates.find((t) => t.id === selected);

  const steps = session
    ? setupSteps(stepSourceOf(session))
    : setupSteps({
        interaction: selectedTemplate?.interaction ?? "connect",
        hasFixedAuthority: selectedTemplate?.hasFixedAuthority ?? true,
      });
  const index = Math.max(0, steps.findIndex((s) => s.id === step));
  const goTo = (offset: number) => setStep(steps[Math.min(steps.length - 1, Math.max(0, index + offset))].id);

  const cancel = async () => {
    if (session) await setupApi.cancel(session.id).catch(() => undefined);
    navigate("/settings/oidc");
  };

  const startWithTemplate = async () => {
    if (!selectedTemplate) return;
    setBusy(true);
    setError(null);
    try {
      // A different template than the open session starts a new session.
      if (session && session.templateId !== selectedTemplate.id) {
        await setupApi.cancel(session.id).catch(() => undefined);
      }
      const s = session && session.templateId === selectedTemplate.id ? session : await setupApi.createForTemplate(selectedTemplate.id);
      setSession(s);
      setParams({ session: s.id }, { replace: true });
      const next = setupSteps(stepSourceOf(s));
      setStep(next[1].id);
    } catch (err) {
      setError(err instanceof Error ? err.message : "The setup could not start.");
    } finally {
      setBusy(false);
    }
  };

  const onSession = useCallback((s: SetupSessionDto) => setSession(s), []);
  const stepProps = session
    ? { session, onSession, onNext: () => goTo(1), onBack: () => goTo(-1), onCancel: cancel }
    : null;

  return (
    <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
      <SettingsBreadcrumb trail={[{ label: "Single Sign-On", to: "/settings/oidc" }, "Add provider"]} />
      <h2 className="mb-5 text-[26px] font-bold leading-[34px] text-fg">Add provider</h2>
      <div className="mb-6">
        <Stepper steps={steps.map((s) => s.label)} current={index} testId="setup-steps" />
      </div>

      <section className="flex max-w-[860px] flex-col gap-[22px] rounded-2xl border border-line bg-surface px-6 pb-6 pt-6" data-testid={`step-${step}`}>
        {error && <Alert tone="error" title="Something went wrong">{error}</Alert>}

        {step === "template" && (
          <>
            <StepHeading
              title="Choose a template"
              description="A template knows the provider's address, how to register ReadyStackGo and which details it sends."
            />
            <div role="radiogroup" aria-label="Templates" className="grid gap-4 sm:grid-cols-2">
              {templates.map((t, i) => (
                <SignInOptionCard
                  key={t.id}
                  title={t.name}
                  description={t.description}
                  mark={<ProviderMark iconUrl={t.iconUrl} />}
                  selected={t.id === selected}
                  onSelect={() => setSelected(t.id)}
                  tabIndex={t.id === selected || (!selected && i === 0) ? 0 : -1}
                  onKeyDown={(e) => templateKeys.onKeyDown(e, i)}
                  buttonRef={templateKeys.setRef(i)}
                  testId={`template-${t.id}`}
                />
              ))}
            </div>
            <p className="text-xs text-fg-muted">Templates come with ReadyStackGo or from the templates directory of this installation.</p>
            <StepFooter onCancel={cancel} busy={busy} primaryDisabled={!selectedTemplate} onPrimary={startWithTemplate} />
          </>
        )}

        {stepProps && step === "provider" && <ProviderAddressStep {...stepProps} />}
        {stepProps && step === "installation" && <InstallationStep key={session!.id} {...stepProps} />}
        {stepProps && step === "connect" && <ConnectStep {...stepProps} />}
        {stepProps && step === "register" && <RegisterStep {...stepProps} />}
        {stepProps && step === "test" && <TestStep {...stepProps} />}
        {stepProps && step === "save" && (
          <SaveStep
            {...stepProps}
            onSaved={(name) => navigate(`/settings/oidc?saved=${encodeURIComponent(name)}`)}
          />
        )}
      </section>
    </div>
  );
}

import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import WizardLayout from './WizardLayout';
import AdminStep from './AdminStep';
import OrganizationStep from './OrganizationStep';
import ConnectionsStep from './ConnectionsStep';
import InstallStep from './InstallStep';
import { createAdmin, setOrganization, setConnections, installStack } from '../../api/wizard';

export default function Wizard() {
  const [currentStep, setCurrentStep] = useState(1);
  const navigate = useNavigate();

  const handleAdminNext = async (data: { username: string; password: string }) => {
    await createAdmin(data);
    setCurrentStep(2);
  };

  const handleOrganizationNext = async (data: { id: string; name: string }) => {
    await setOrganization(data);
    setCurrentStep(3);
  };

  const handleConnectionsNext = async (data: { transport: string; persistence: string; eventStore?: string }) => {
    await setConnections(data);
    setCurrentStep(4);
  };

  const handleInstall = async () => {
    const result = await installStack();
    if (result.success) {
      // Installation successful, redirect to login
      navigate('/login');
    } else {
      throw new Error(result.errors.join(', ') || 'Installation failed');
    }
  };

  const handleBack = () => {
    setCurrentStep(Math.max(1, currentStep - 1));
  };

  return (
    <WizardLayout currentStep={currentStep} totalSteps={4}>
      {currentStep === 1 && <AdminStep onNext={handleAdminNext} />}
      {currentStep === 2 && <OrganizationStep onNext={handleOrganizationNext} onBack={handleBack} />}
      {currentStep === 3 && <ConnectionsStep onNext={handleConnectionsNext} onBack={handleBack} />}
      {currentStep === 4 && <InstallStep onInstall={handleInstall} onBack={handleBack} />}
    </WizardLayout>
  );
}

import { apiGet, apiPost, apiPut } from './client';

export interface BuildInfo {
  gitCommit?: string;
  buildDate?: string;
  runtimeVersion?: string;
}

export interface VersionInfo {
  serverVersion: string;
  updateAvailable: boolean;
  latestVersion?: string;
  latestReleaseUrl?: string;
  build: BuildInfo;
}

export interface CertificateInfo {
  subject: string;
  issuer: string;
  expiresAt: string;
  thumbprint: string;
  isSelfSigned: boolean;
  isExpired: boolean;
  isExpiringSoon: boolean;
}

export type ReverseProxySslMode = 'Termination' | 'Passthrough' | 'ReEncryption';

export interface ReverseProxyConfig {
  enabled: boolean;
  sslMode: ReverseProxySslMode;
  trustForwardedFor: boolean;
  trustForwardedProto: boolean;
  trustForwardedHost: boolean;
  knownProxies: string[];
  forwardLimit?: number;
  pathBase?: string;
}

export interface TlsConfig {
  mode: string;
  certificateInfo?: CertificateInfo;
  httpEnabled: boolean;
  reverseProxy?: ReverseProxyConfig;
}

export interface ReverseProxyUpdate {
  enabled?: boolean;
  sslMode?: ReverseProxySslMode;
  trustForwardedFor?: boolean;
  trustForwardedProto?: boolean;
  trustForwardedHost?: boolean;
  knownProxies?: string[];
  forwardLimit?: number;
  pathBase?: string;
}

export interface UpdateTlsConfigRequest {
  pfxBase64?: string;
  pfxPassword?: string;
  certificatePem?: string;
  privateKeyPem?: string;
  httpEnabled?: boolean;
  resetToSelfSigned?: boolean;
  reverseProxy?: ReverseProxyUpdate;
}

export interface UpdateTlsConfigResponse {
  success: boolean;
  message?: string;
  requiresRestart: boolean;
}

// Let's Encrypt types
export type LetsEncryptChallengeType = 'Http01' | 'Dns01';
export type LetsEncryptDnsProviderType = 'Manual' | 'Cloudflare';

export interface PendingDnsChallenge {
  domain: string;
  txtRecordName: string;
  txtValue: string;
  createdAt: string;
}

export interface LetsEncryptStatus {
  isConfigured: boolean;
  isActive: boolean;
  domains: string[];
  certificateExpiresAt?: string;
  lastIssuedAt?: string;
  lastRenewalAttempt?: string;
  lastError?: string;
  isUsingStaging: boolean;
  challengeType: string;
  pendingDnsChallenges: PendingDnsChallenge[];
}

export interface LetsEncryptDnsProviderConfig {
  type: LetsEncryptDnsProviderType;
  cloudflareApiToken?: string;
  cloudflareZoneId?: string;
}

export interface ConfigureLetsEncryptRequest {
  domains: string[];
  email: string;
  useStaging: boolean;
  challengeType: LetsEncryptChallengeType;
  dnsProvider?: LetsEncryptDnsProviderConfig;
}

export interface ConfigureLetsEncryptResponse {
  success: boolean;
  message?: string;
  expiresAt?: string;
  requiresRestart: boolean;
  awaitingManualDnsChallenge: boolean;
}

export const systemApi = {
  getVersion: () => apiGet<VersionInfo>('/api/system/version'),
  getTlsConfig: () => apiGet<TlsConfig>('/api/system/tls'),
  updateTlsConfig: (request: UpdateTlsConfigRequest) =>
    apiPut<UpdateTlsConfigResponse>('/api/system/tls', request),

  // Let's Encrypt
  getLetsEncryptStatus: () => apiGet<LetsEncryptStatus>('/api/system/tls/letsencrypt'),
  configureLetsEncrypt: (request: ConfigureLetsEncryptRequest) =>
    apiPost<ConfigureLetsEncryptResponse>('/api/system/tls/letsencrypt', request),
  confirmLetsEncryptDns: () =>
    apiPost<ConfigureLetsEncryptResponse>('/api/system/tls/letsencrypt/confirm-dns', {}),
};

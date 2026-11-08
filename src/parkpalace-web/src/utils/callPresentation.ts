import { CallRecordDto } from '../types';

export interface CallPerspective {
  number: string;
  name?: string;
  direction: string;
  outgoing: boolean;
}

export function getCallPerspective(call: CallRecordDto, viewerExtension?: string): CallPerspective {
  const internal = call.scope === 2 || call.direction === 2;
  const viewer = (viewerExtension || '').trim();
  const callerExtension = (call.extension || '').trim();
  const outgoingInternal = internal && viewer.length > 0 && callerExtension === viewer;

  if (internal) {
    return {
      number: outgoingInternal ? call.destinationNumber : call.callerNumber,
      name: outgoingInternal ? call.internalDestinationName : call.internalCallerName,
      direction: outgoingInternal ? 'خروجی داخلی' : 'ورودی داخلی',
      outgoing: outgoingInternal,
    };
  }

  const inbound = call.direction === 0;
  return {
    number: inbound ? call.callerNumber : call.destinationNumber,
    name: call.callerName || call.partyName,
    direction: inbound ? 'ورودی' : call.direction === 1 ? 'خروجی' : 'داخلی',
    outgoing: call.direction === 1,
  };
}

export type StatusType = 'green' | 'amber' | 'red' | null;

export interface PoiItem {
  name: string;
  distKm: string;
  placeId: string;
  mapsUrl: string;
}

export interface CostItem {
  api:      string;
  calls:    number;
  unitCost: number;   // USD
  total:    number;
  status:   'ok' | 'skipped' | 'error';
  note?:    string;
}

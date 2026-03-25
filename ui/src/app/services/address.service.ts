import { Injectable } from '@angular/core';
import { environment } from '../../environments/environment';

export interface ValidationResult {
  formattedAddress: string;
  postalCode: string;
  granularity: string;
  geocodeGranularity: string;
  addressComplete: boolean;
  lat: number | null;
  lng: number | null;
}

export interface PlaceResult {
  name: string;
  placeId: string;
  geometry: { location: { lat: number; lng: number } };
}

export interface PostalBoundaryTarget {
  placeId: string;
  location: { lat: number; lng: number };
  viewport: {
    northEast: { lat: number; lng: number };
    southWest: { lat: number; lng: number };
  } | null;
  formattedAddress: string;
}

@Injectable({ providedIn: 'root' })
export class AddressService {

  private readonly apiBaseUrl = environment.apiBaseUrl;

  /** Haversine distance in km */
  haversine(lat1: number, lng1: number, lat2: number, lng2: number): number {
    const R = 6371;
    const dLat = (lat2 - lat1) * Math.PI / 180;
    const dLng = (lng2 - lng1) * Math.PI / 180;
    const a = Math.sin(dLat / 2) ** 2 +
              Math.cos(lat1 * Math.PI / 180) * Math.cos(lat2 * Math.PI / 180) *
              Math.sin(dLng / 2) ** 2;
    return R * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
  }

  /** Step 1 — Google Address Validation */
  async validateAddress(
    address: string,
    city: string,
    state: string,
    iso2: string,
  ): Promise<ValidationResult> {
    const url = new URL('/api/address-validation', this.apiBaseUrl);
    const res = await fetch(url.toString(), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ address, city, state, iso2 }),
    });

    if (!res.ok) {
      throw new Error(await this.extractErrorMessage(res));
    }

    return await res.json();
  }

  /** Step 2 — Google Geocoding postal boundary target lookup */
  async fetchPostalBoundaryTarget(postalCode: string, iso2: string): Promise<PostalBoundaryTarget> {
    const url = new URL('/api/postal-boundary-target', this.apiBaseUrl);
    url.searchParams.set('postalCode', postalCode.trim());
    url.searchParams.set('iso2', iso2.trim());

    const res = await fetch(url.toString());

    if (!res.ok) {
      throw new Error(await this.extractErrorMessage(res));
    }

    return await res.json();
  }

  /** Steps 3 & 4 — Places API (New): Nearby Search for airports, Text Search for ports */
  async nearbySearch(lat: number, lng: number, type: string | null, keyword: string | null): Promise<PlaceResult[]> {
    let url: string;
    let body: object;

    if (type) {
      url = new URL('/api/places/nearby-search', this.apiBaseUrl).toString();
      body = { lat, lng, type };
    } else {
      url = new URL('/api/places/text-search', this.apiBaseUrl).toString();
      body = { lat, lng, keyword: keyword || 'port harbor seaport' };
    }

    const res = await fetch(url, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(body),
    });

    if (!res.ok) {
      throw new Error(await this.extractErrorMessage(res));
    }

    return await res.json();
  }

  private async extractErrorMessage(res: Response): Promise<string> {
    const bodyText = await res.text();
    let message = `API ${res.status}`;

    if (!bodyText) return message;

    try {
      const payload = JSON.parse(bodyText);
      return payload?.error || payload?.detail || payload?.title || message;
    } catch {
      return `${message}: ${bodyText.slice(0, 200)}`;
    }
  }
}

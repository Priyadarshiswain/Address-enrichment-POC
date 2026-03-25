import { Injectable } from '@angular/core';
import { environment } from '../../environments/environment';

declare const google: any;

interface ClientConfig {
  mapsApiKey: string;
  mapId: string;
}

@Injectable({ providedIn: 'root' })
export class MapsService {

  private loaded = false;
  private loadPromise: Promise<void> | null = null;
  private clientConfigPromise: Promise<ClientConfig> | null = null;

  load(): Promise<void> {
    if (this.loaded) return Promise.resolve();
    if (this.loadPromise) return this.loadPromise;

    this.loadPromise = this.fetchClientConfig().then((config) => new Promise<void>((resolve, reject) => {
      (window as any)['__mapsReady'] = () => {
        this.loaded = true;
        resolve();
      };

      const script = document.createElement('script');
      script.src = `https://maps.googleapis.com/maps/api/js?key=${config.mapsApiKey}&callback=__mapsReady&loading=async`;
      script.onerror = () => reject(new Error('Failed to load Google Maps'));
      document.head.appendChild(script);
    }));

    return this.loadPromise;
  }

  async createMap(el: HTMLElement, lat: number, lng: number): Promise<any> {
    const config = await this.fetchClientConfig();

    return new google.maps.Map(el, {
      center: { lat, lng },
      zoom: 12,
      mapTypeControl: false,
      streetViewControl: false,
      fullscreenControl: true,
      ...(config.mapId ? { mapId: config.mapId } : {}),
    });
  }

  addAddressPin(map: any, lat: number, lng: number, title: string): any {
    const marker = new google.maps.Marker({
      position: { lat, lng },
      map,
      title,
      icon: {
        path: google.maps.SymbolPath.CIRCLE,
        scale: 10,
        fillColor: '#D40511',
        fillOpacity: 1,
        strokeColor: '#fff',
        strokeWeight: 2,
      },
      zIndex: 10,
    });
    const info = new google.maps.InfoWindow({ content: `<strong>${title}</strong>` });
    marker.addListener('click', () => info.open(map, marker));
    return marker;
  }

  addPoiMarker(map: any, lat: number, lng: number, name: string, dist: string, type: 'airport' | 'port'): any {
    const label = type === 'airport' ? '✈' : '⚓';
    const color = type === 'airport' ? '#1565C0' : '#00695C';
    const marker = new google.maps.Marker({
      position: { lat, lng },
      map,
      title: name,
      label: { text: label, fontSize: '16px' },
      icon: {
        path: google.maps.SymbolPath.CIRCLE,
        scale: 14,
        fillColor: color,
        fillOpacity: 0.9,
        strokeColor: '#fff',
        strokeWeight: 2,
      },
    });
    const info = new google.maps.InfoWindow({
      content: `<strong>${name}</strong><br>${dist} km away`,
    });
    marker.addListener('click', () => info.open(map, marker));
    return marker;
  }

  async drawPostalBoundary(map: any, placeId: string): Promise<{ ok: boolean; error?: string }> {
    const config = await this.fetchClientConfig();
    if (!config.mapId) {
      return {
        ok: false,
        error: 'Google Maps boundary rendering requires a vector map ID with POSTAL_CODE boundaries enabled.',
      };
    }

    if (typeof map.getFeatureLayer !== 'function') {
      return {
        ok: false,
        error: 'This map instance does not support Google boundary feature layers.',
      };
    }

    const featureType = google.maps.FeatureType?.POSTAL_CODE ?? 'POSTAL_CODE';
    const featureLayer = map.getFeatureLayer(featureType);

    if (!featureLayer) {
      return {
        ok: false,
        error: 'Google postal boundary feature layer is unavailable on this map.',
      };
    }

    if (featureLayer.isAvailable === false) {
      return {
        ok: false,
        error: 'Google postal boundary layer is not enabled for the configured map ID.',
      };
    }

    featureLayer.style = ({ feature }: any) => {
      if (feature.placeId !== placeId) {
        return undefined;
      }

      return {
        strokeColor: '#FFCC00',
        strokeOpacity: 0.95,
        strokeWeight: 2,
        fillColor: '#FFCC00',
        fillOpacity: 0.18,
      };
    };

    return { ok: true };
  }

  fitViewport(map: any, viewport: { northEast: { lat: number; lng: number }; southWest: { lat: number; lng: number } } | null): void {
    if (!viewport) return;

    const bounds = new google.maps.LatLngBounds(
      { lat: viewport.southWest.lat, lng: viewport.southWest.lng },
      { lat: viewport.northEast.lat, lng: viewport.northEast.lng },
    );

    map.fitBounds(bounds);
  }

  fitBounds(map: any, objects: any[]): void {
    const bounds = new google.maps.LatLngBounds();
    objects.forEach(obj => {
      if (obj?.getPosition) bounds.extend(obj.getPosition());
    });
    if (!bounds.isEmpty()) map.fitBounds(bounds);
  }

  clearObjects(objects: any[]): void {
    objects.forEach(obj => obj?.setMap(null));
  }

  private fetchClientConfig(): Promise<ClientConfig> {
    if (this.clientConfigPromise) return this.clientConfigPromise;

    const url = new URL('/api/config', environment.apiBaseUrl).toString();
    this.clientConfigPromise = fetch(url).then(async (res) => {
      if (!res.ok) {
        throw new Error('Failed to load map config');
      }

      const data = await res.json();
      if (!data?.mapsApiKey) {
        throw new Error('Map config did not include mapsApiKey');
      }

      return {
        mapsApiKey: data.mapsApiKey as string,
        mapId: (data.mapId as string) || '',
      };
    });

    return this.clientConfigPromise;
  }
}

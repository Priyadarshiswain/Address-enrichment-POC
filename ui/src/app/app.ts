import { Component, ViewChild, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AddressService, PlaceResult } from './services/address.service';
import { MapsService } from './services/maps.service';
import { COUNTRIES } from './models/countries';
import { StatusType, PoiItem, CostItem } from './models/index';
import { AddressFormComponent } from './components/address-form/address-form.component';
import { MapViewComponent } from './components/map-view/map-view.component';
import { PoiPanelComponent } from './components/poi-panel/poi-panel.component';
import { CostSummaryComponent } from './components/cost-summary/cost-summary.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, AddressFormComponent, MapViewComponent, PoiPanelComponent, CostSummaryComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App implements OnDestroy {

  @ViewChild(MapViewComponent) mapViewRef?: MapViewComponent;

  /* ── State ── */
  loading        = false;
  statusType: StatusType = null;
  statusMsg      = '';
  stdAddress     = '';
  postalCodeOut  = '';
  showMap        = false;
  showPanels     = false;

  airports:       PoiItem[] = [];
  ports:          PoiItem[] = [];
  airportError   = '';
  portError      = '';
  boundaryError  = '';

  costItems:        CostItem[] = [];
  totalCost         = 0;
  showCost          = false;
  freeRequestsLeft  = 0;   // how many more requests $200 credit covers
  netMonthlyCost    = 0;   // after $200 credit at 30k req/month
  GOOGLE_FREE_CREDIT = 200;

  /* ── Map inputs ── */
  mapLat          = 0;
  mapLng          = 0;
  mapPostalCode   = '';
  mapCountryIso2  = '';
  mapFormattedAddress = '';

  private airportPlaces: PlaceResult[] = [];
  private portPlaces:    PlaceResult[] = [];

  constructor(
    private addrSvc: AddressService,
    private mapsSvc: MapsService,
  ) {}

  /* ── Main action ── */
  async validate(event: { address: string; city: string; state: string; country: string }): Promise<void> {
    const { address, city, state, country } = event;

    if (!address.trim() || !city.trim() || !country.trim()) {
      alert('Please fill in Shipper Address, City and Country.');
      return;
    }

    const countryObj = COUNTRIES.find(
      c => c.name.toLowerCase() === country.trim().toLowerCase()
    );
    if (!countryObj) {
      alert(`Country "${country}" not recognised. Please select from the list.`);
      return;
    }

    /* Reset */
    this.statusType   = null;
    this.statusMsg    = '';
    this.stdAddress   = '';
    this.postalCodeOut = '';
    this.showMap      = false;
    this.showPanels   = false;
    this.airports     = [];
    this.ports        = [];
    this.airportError  = '';
    this.portError     = '';
    this.boundaryError = '';
    this.costItems     = [];
    this.totalCost     = 0;
    this.showCost      = false;
    this.airportPlaces = [];
    this.portPlaces    = [];
    this.loading = true;

    try {

      /* Step 1 — Address Validation */
      let result;
      try {
        result = await this.addrSvc.validateAddress(address, city, state, countryObj.iso2);
        this.addCost('Google Address Validation API', 1, 0.0025, 'ok');
      } catch (e: any) {
        this.addCost('Google Address Validation API', 1, 0.0025, 'error');
        this.setStatus('red', `Address Validation failed: ${e.message}`);
        return;
      }

      this.stdAddress = result.formattedAddress;

      // Use the better of validationGranularity vs geocodeGranularity
      const gran = result.granularity.toUpperCase() || result.geocodeGranularity.toUpperCase();

      const GREEN_LEVELS = ['SUB_PREMISE', 'PREMISE', 'PREMISE_PROXIMITY'];
      const AMBER_LEVELS = ['BLOCK', 'ROUTE'];

      if (GREEN_LEVELS.includes(gran)) {
        this.setStatus('green', 'Address confirmed — high confidence match.');
      } else if (AMBER_LEVELS.includes(gran) || result.addressComplete) {
        this.setStatus('amber', 'Address partially matched — verify before use.');
      } else if (result.lat !== null) {
        // geocode succeeded even if validation granularity is low — show map with warning
        this.setStatus('amber', `Address matched at low confidence (${gran || 'unknown'}) — verify before use.`);
      } else {
        this.setStatus('red', 'Address not recognised — please check and re-enter.');
        return;
      }

      this.postalCodeOut = result.postalCode || '(not returned)';

      if (result.lat === null || result.lng === null) {
        this.setStatus('amber', 'Address matched but no geocode returned — map unavailable.');
        return;
      }

      const { lat, lng } = result as { lat: number; lng: number };

      /* Step 2 — load Maps SDK, set map inputs */
      await this.mapsSvc.load();
      this.addCost('Google Maps JavaScript API', 1, 0.007, 'ok', 'Per map load');

      this.mapLat             = lat;
      this.mapLng             = lng;
      this.mapPostalCode      = result.postalCode || '';
      this.mapCountryIso2     = countryObj.iso2;
      this.mapFormattedAddress = result.formattedAddress;
      this.showMap            = true;

      /* Wait for *ngIf to render the MapViewComponent */
      await this.tick();

      /* Steps 3 & 4 — airports + ports (parallel, non-fatal) */
      this.showPanels = true;

      const [airRes, portRes] = await Promise.allSettled([
        this.addrSvc.nearbySearch(lat, lng, 'airport', null),
        this.addrSvc.nearbySearch(lat, lng, null, 'port harbor seaport'),
      ]);

      if (airRes.status === 'fulfilled') {
        this.airportPlaces = airRes.value;
        this.airports = this.buildPoiList(airRes.value, lat, lng);
        this.airports.forEach(a => {
          const p = airRes.value.find(r => r.placeId === a.placeId)!;
          this.mapViewRef?.addPoiMarker(
            p.geometry.location.lat, p.geometry.location.lng,
            a.name, a.distKm, 'airport'
          );
        });
        this.addCost('Places API (New) — Nearby Search', 1, 0.032, 'ok', 'Airports within 50 km');
      } else {
        this.airportError = `Could not load airports: ${(airRes as any).reason?.message}`;
        this.addCost('Places API (New) — Nearby Search', 1, 0.032, 'error');
      }

      if (portRes.status === 'fulfilled') {
        this.portPlaces = portRes.value;
        this.ports = this.buildPoiList(portRes.value, lat, lng);
        this.ports.forEach(a => {
          const p = portRes.value.find(r => r.placeId === a.placeId)!;
          this.mapViewRef?.addPoiMarker(
            p.geometry.location.lat, p.geometry.location.lng,
            a.name, a.distKm, 'port'
          );
        });
        this.addCost('Places API (New) — Text Search', 1, 0.032, 'ok', 'Ports within 50 km');
      } else {
        this.portError = `Could not load ports: ${(portRes as any).reason?.message}`;
        this.addCost('Places API (New) — Text Search', 1, 0.032, 'error');
      }

      this.totalCost        = this.costItems.reduce((s, c) => s + c.total, 0);
      this.freeRequestsLeft = this.totalCost > 0
        ? Math.floor(this.GOOGLE_FREE_CREDIT / this.totalCost)
        : Infinity;
      const monthlyGross    = this.totalCost * 30000;
      this.netMonthlyCost   = Math.max(0, monthlyGross - this.GOOGLE_FREE_CREDIT);
      this.showCost         = true;

      this.mapViewRef?.fitBounds();

    } catch (e: any) {
      this.setStatus('red', `Unexpected error: ${e.message}`);
      console.error(e);
    } finally {
      this.loading = false;
    }
  }

  /* ── Boundary status from MapViewComponent ── */
  onBoundaryStatus(event: { error: string; costNote: string }): void {
    if (event.error) {
      this.boundaryError = event.error;
    }
    const status = event.costNote === 'ok' ? 'ok'
                 : event.costNote === 'skipped' ? 'skipped'
                 : 'error';
    this.addCost('Google Maps Boundaries', event.costNote === 'skipped' ? 0 : 1, 0, status,
      event.costNote === 'ok' ? 'Google POSTAL_CODE feature layer'
      : event.error || undefined);
  }

  /* ── Helpers ── */
  private setStatus(type: StatusType, msg: string): void {
    this.statusType = type;
    this.statusMsg  = msg;
  }

  private buildPoiList(places: PlaceResult[], addrLat: number, addrLng: number): PoiItem[] {
    return places
      .map(p => ({
        place: p,
        dist: this.addrSvc.haversine(addrLat, addrLng, p.geometry.location.lat, p.geometry.location.lng),
      }))
      .filter(({ dist }) => dist <= 50)          // hard 50 km cutoff
      .sort((a, b) => a.dist - b.dist)           // nearest first
      .slice(0, 3)
      .map(({ place: p, dist }) => ({
        name:    p.name,
        distKm:  dist.toFixed(1),
        placeId: p.placeId,
        mapsUrl: `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(p.name)}&query_place_id=${p.placeId}`,
      }));
  }

  private addCost(api: string, calls: number, unitCost: number, status: CostItem['status'], note?: string): void {
    this.costItems.push({ api, calls, unitCost, total: calls * unitCost, status, note });
  }

  private tick(): Promise<void> {
    return new Promise(r => setTimeout(r, 0));
  }

  ngOnDestroy(): void {
    // MapViewComponent handles its own cleanup via ngOnDestroy
  }
}

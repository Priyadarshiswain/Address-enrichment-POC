import {
  Component,
  Input,
  Output,
  EventEmitter,
  ViewChild,
  ElementRef,
  OnChanges,
  SimpleChanges,
  OnDestroy,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { MapsService } from '../../services/maps.service';
import { AddressService } from '../../services/address.service';
import { PoiItem } from '../../models/index';

@Component({
  selector: 'app-map-view',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './map-view.component.html',
  styleUrl: './map-view.component.scss',
})
export class MapViewComponent implements OnChanges, OnDestroy {
  @Input() lat: number = 0;
  @Input() lng: number = 0;
  @Input() postalCode: string = '';
  @Input() countryIso2: string = '';
  @Input() airports: PoiItem[] = [];
  @Input() ports: PoiItem[] = [];
  @Input() boundaryError: string = '';
  @Input() formattedAddress: string = '';

  @Output() boundaryStatusChange = new EventEmitter<{ error: string; costNote: string }>();

  @ViewChild('mapEl') mapElRef!: ElementRef<HTMLDivElement>;

  private googleMap: any = null;
  private mapObjects: any[] = [];

  constructor(
    private mapsSvc: MapsService,
    private addrSvc: AddressService,
  ) {}

  async ngOnChanges(changes: SimpleChanges): Promise<void> {
    if (!changes['lat'] && !changes['lng']) return;
    if (!this.lat || !this.lng) return;

    // Wait for the view to render the map div
    await this.tick();

    if (!this.mapElRef?.nativeElement) return;

    // Clear any previous map objects
    this.mapsSvc.clearObjects(this.mapObjects);
    this.mapObjects = [];
    this.googleMap = null;

    await this.mapsSvc.load();

    this.googleMap = await this.mapsSvc.createMap(this.mapElRef.nativeElement, this.lat, this.lng);
    const pin = this.mapsSvc.addAddressPin(this.googleMap, this.lat, this.lng, this.formattedAddress);
    this.mapObjects.push(pin);

    /* Boundary */
    if (this.postalCode) {
      try {
        const target = await this.addrSvc.fetchPostalBoundaryTarget(this.postalCode, this.countryIso2);
        console.debug('[Boundary] target received:', target);
        this.mapsSvc.fitViewport(this.googleMap, target.viewport);

        const result = await this.mapsSvc.drawPostalBoundary(this.googleMap, target.placeId);
        if (result.ok) {
          console.debug('[Boundary] Google POSTAL_CODE layer styled for', this.postalCode, this.countryIso2);
          this.boundaryStatusChange.emit({ error: '', costNote: 'ok' });
        } else {
          this.boundaryStatusChange.emit({
            error: result.error || 'Postal boundary returned no drawable geometry.',
            costNote: 'skipped',
          });
        }
      } catch (e: any) {
        this.boundaryStatusChange.emit({ error: `Postal boundary unavailable: ${e.message}`, costNote: 'error' });
        console.warn('[Google postal boundary] failed:', e.message);
      }
    } else {
      this.boundaryStatusChange.emit({ error: 'No postal code — boundary skipped.', costNote: 'skipped' });
    }
  }

  /** Called by the parent (App) after it has resolved POI lat/lng from PlaceResult data */
  addPoiMarker(lat: number, lng: number, name: string, distKm: string, type: 'airport' | 'port'): void {
    if (!this.googleMap) return;
    const m = this.mapsSvc.addPoiMarker(this.googleMap, lat, lng, name, distKm, type);
    this.mapObjects.push(m);
  }

  fitBounds(): void {
    if (this.googleMap) {
      this.mapsSvc.fitBounds(this.googleMap, this.mapObjects);
    }
  }

  private tick(): Promise<void> {
    return new Promise(r => setTimeout(r, 0));
  }

  ngOnDestroy(): void {
    this.mapsSvc.clearObjects(this.mapObjects);
    this.mapObjects = [];
    this.googleMap = null;
  }
}

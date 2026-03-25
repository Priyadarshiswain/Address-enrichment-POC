import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { COUNTRIES, Country } from '../../models/countries';
import { StatusType } from '../../models/index';

@Component({
  selector: 'app-address-form',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './address-form.component.html',
  styleUrl: './address-form.component.scss',
})
export class AddressFormComponent {
  @Input() loading: boolean = false;
  @Input() postalCodeOut: string = '';
  @Input() statusType: StatusType = null;
  @Input() statusMsg: string = '';
  @Input() stdAddress: string = '';

  @Output() validateClicked = new EventEmitter<{
    address: string;
    city: string;
    state: string;
    country: string;
  }>();

  shipperAddress = '';
  city = '';
  state = '';
  countryInput = '';

  filteredCountries: Country[] = [];
  showDropdown = false;

  readonly countries = COUNTRIES;

  /* ── Country autocomplete ── */
  onCountryInput(): void {
    const q = this.countryInput.trim().toLowerCase();
    if (!q) { this.filteredCountries = []; this.showDropdown = false; return; }
    this.filteredCountries = this.countries
      .filter(c => c.name.toLowerCase().includes(q))
      .slice(0, 8);
    this.showDropdown = this.filteredCountries.length > 0;
  }

  selectCountry(c: Country): void {
    this.countryInput = c.name;
    this.showDropdown = false;
    this.filteredCountries = [];
  }

  hideDropdown(): void {
    setTimeout(() => { this.showDropdown = false; }, 150);
  }

  onValidate(): void {
    this.validateClicked.emit({
      address: this.shipperAddress,
      city: this.city,
      state: this.state,
      country: this.countryInput,
    });
  }
}

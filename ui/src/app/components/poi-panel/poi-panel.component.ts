import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PoiItem } from '../../models/index';

@Component({
  selector: 'app-poi-panel',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './poi-panel.component.html',
  styleUrl: './poi-panel.component.scss',
})
export class PoiPanelComponent {
  @Input() title: string = '';
  @Input() radiusLabel: string = '';
  @Input() items: PoiItem[] = [];
  @Input() error: string = '';
}

import { Component, Input } from '@angular/core';
import { CommonModule, DecimalPipe } from '@angular/common';
import { CostItem } from '../../models/index';

@Component({
  selector: 'app-cost-summary',
  standalone: true,
  imports: [CommonModule, DecimalPipe],
  templateUrl: './cost-summary.component.html',
  styleUrl: './cost-summary.component.scss',
})
export class CostSummaryComponent {
  @Input() costItems: CostItem[] = [];
  @Input() totalCost: number = 0;
  @Input() freeRequestsLeft: number = 0;
  @Input() netMonthlyCost: number = 0;

  expanded = false;

  toggleExpanded(): void {
    this.expanded = !this.expanded;
  }
}

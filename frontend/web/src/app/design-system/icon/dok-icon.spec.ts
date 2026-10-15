import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { DokIcon } from './dok-icon';

@Component({ imports: [DokIcon], template: `<dok-icon name="stop" /><dok-icon name="pause" />` })
class Host {}

describe('dok-icon', () => {
  it('fills the stop square and keeps the other icons as strokes', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const [stop, pause] = Array.from<SVGElement>(fixture.nativeElement.querySelectorAll('svg'));
    expect(stop.getAttribute('fill')).toBe('currentColor');
    expect(pause.getAttribute('fill')).toBe('none');
    expect(stop.querySelector('path')!.getAttribute('d')).not.toBe(pause.querySelector('path')!.getAttribute('d'));
  });
});

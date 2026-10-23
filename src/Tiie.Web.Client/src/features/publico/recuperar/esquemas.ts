import { z } from 'zod';
import { campoCorreo, campoCurp } from '../comun/validaciones';

export const esquemaRecuperar = z.object({ curp: campoCurp, correo: campoCorreo });
export type ValoresRecuperar = z.infer<typeof esquemaRecuperar>;

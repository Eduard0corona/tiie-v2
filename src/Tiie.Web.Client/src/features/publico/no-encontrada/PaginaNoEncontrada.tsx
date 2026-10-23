import { Link } from 'react-router';
import { estiloBoton } from '@/components/ui/Boton';
import { EstadoVacio } from '../comun/EstadoVacio';

export function PaginaNoEncontrada() {
  return (
    <EstadoVacio
      icono="ubicacion"
      titulo="Página no encontrada"
      acciones={
        <>
          <Link to="/" className={estiloBoton({ tamano: 'grande' })}>
            Ir al inicio
          </Link>
          <Link to="/agendar" className={estiloBoton({ variante: 'secundario', tamano: 'grande' })}>
            Agendar una cita
          </Link>
        </>
      }
    >
      <p>La dirección que abriste no existe o cambió.</p>
    </EstadoVacio>
  );
}

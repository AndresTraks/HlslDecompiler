xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=5
dcl_texcoord0 r0
/*    0.0 */       alloc colors
/*    0.1 */       exec
/*    2   */          floor r2, r0
/*    3   */          frc r4, r0
              +       log r3._y__, r_abs[0].z
/*    4   */          sgt r5, r0, -r0
              +       exp r3.__z_, r0.w
/*    5   */          sgt r1, -r0, r0
              +       log r3.x___, r_abs[0].x
/*    6   */          mul r3.x___, r3.xxxx, r0.yyyy
              +       rsq r3.___w, r_abs[0].x
/*    7   */          add r1, r5, -r1
              +       exp r3.x___, r3.x
/*    1.0 */       exece
/*    8   */          add r3, r4, r3
/*    9   */          add r2, r3, r2
/*   10   */          add r1, r2, r1
/*   11   */          add oC0, r1, r_abs[0]
/*    1.1 */       cnop

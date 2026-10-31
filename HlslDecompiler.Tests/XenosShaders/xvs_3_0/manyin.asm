xvs_3_0
config AutoSerialize=false
config AutoResource=false
config VsMaxReg=5
config VsResource=1
dcl_index r0.x
/*    0.0 */       exec
/*    3   */          vfetch r1, r0.x, position0
/*    4   */          vfetch r5.xyz_, r0.x, normal0
/*    5   */          vfetch r4.xyz_, r0.x, tangent0
/*    6   */          vfetch r3.xyz_, r0.x, binormal0
/*    7   */          vfetch r2.xy__, r0.x, texcoord0
/*    8   */          vfetch r2.__xy, r0.x, texcoord1
/*    0.1 */       exec
/*    9   */          vfetch r0, r0.x, color0
/*    1.0 */       alloc position
/*    1.1 */       exec
/*   10   */          add r4.xyz_, r5.xyzz, r4.xyzz
/*   11   */          add r3.xyz_, r4.xyzz, r3.xyzz
/*   12   */          add r1.xyz_, r3.xyzz, r1.xyzz
              +       maxs r0._, r1.ww
/*   13   */          add r1.xyz_, r1.xyzz, r2.xyzz
              +       adds_prev r1.___w, r2.w
/*   14   */          add oPos, r1, r0
/*    2.0 */       alloc interpolators
/*    2.1 */       exece

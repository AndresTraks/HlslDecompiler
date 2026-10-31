xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=1
dcl_texcoord0 r0
/*    0.0 */       alloc colors
/*    0.1 */       exece
/*    1   */          trunc r1.xy__, r0.xyyy
/*    2   */          maxas r0._, r1.xx
/*    3   */          max r0, c[0+a0], c[0+a0]
              +       maxas r0._, r1.yy
/*    4   */          add oC0, r0, c[3+a0]

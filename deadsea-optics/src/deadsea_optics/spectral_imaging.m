% spectral image reconstruction from FRA400 solar configuration
% All units in mm
%% Define reconstruction system model parameter
% camera settings
pixel_size_native=3.45e-3;
pixel_size_recon=2*pixel_size_native;
num_pixels=127; % number of pixels in the reconstructed hyperspectral image
x=-floor(num_pixels/2):floor((num_pixels-1)/2);
y=x;
[X,Y]=meshgrid(x,y);
% fiber spectrometer PSF
beam_waist=25e-3; % 50um fiber core size
gaussian_order=8;
sigma=beam_waist/pixel_size_recon;
PSF_boxing_wdith=2*sigma;

ref_image_ind=1;
num_images=4096; % number of images/spectrum acquired
num_wavelength=2027; % number of wavelength channels in each spectrum from usb2000zz.py
image_prefix='solar';
spectrum_prefix='spectrum';

%% Build system matrix
% (xoffSet, yoffSet) denote the plate-solved coordinates in pixels relative to
% the reference image
xoffset_save=zeros(num_images,1);
yoffset_save=zeros(num_images,1);
img_ref=imread(['./results/',image_prefix,'_',num2str(ref_image_ind),'.png']);
A=zeros(num_images,numel(X));
for ind_image=1:num_images
% xoffSet=0;
% yoffSet=0;
% Plate-solve each image with xcorr
I=imread([image_prefix,'_',num2str(ind_image),'.png']);
img_corr=normxcorr2(double(img_ref),double(I));
[~,index_img_corr]=max(img_corr(:));
[ypeak,xpeak]=ind2sub(size(img_corr),index_img_corr);
yoffSet = (ypeak-size(img_ref,1))/(pixel_size_recon/pixel_size_native);
xoffSet = (xpeak-size(img_ref,2))/(pixel_size_recon/pixel_size_native);
% Fiber PSF of each acquisition
PSF=exp(-(sqrt((X-xoffSet).^2+(Y-yoffSet).^2)/sigma).^gaussian_order);
PSF_boxing_mask=abs(X-x_loc)<PSF_boxing_wdith & abs(Y-y_loc)<PSF_boxing_wdith;
PSF=PSF.*PSF_boxing_mask;
PSF=PSF/sum(abs(PSF(:)));

% figure(1);
% imagesc(x,y,PSF);colorbar;
% axis image;colormap jet;
A(ind_image,:)=PSF(:).';
xoffset_save(ind_image)=xoffset;
yoffset_save(ind_image)=yoffSet;
end
A=sparse(A);

figure(2);
imagesc(A);colorbar;axis image;
title('System matrix A');
figure(3);
scatter(xoffset_save,yoffset_save,4,1:num_images,'filled');
axis square;colormap jet;
title('Sampling points');

%% Reconstruct spectrum
ifu=zeros(num_wavelength,num_images);
for ind_image=1:num_images
load(['./results/',spectrum_prefix,'_',num2str(ind_image),'.mat'],'spectrum');
ifu(:,ind_image)=spectrum;
end
image_recon_ifu=A.\ifu;
image_recon=reshape(image_recon_ifu,[length(y),length(x),-1]);

ind_wavelength=1000; % wavelength for display
figure(4);
imagesc(image_recon(:,:,ind_wavelength));colorbar;
axis image;colormap gray;
% spectral image reconstruction from FRA400 solar configuration
% All units in mm
clear;close all;clc;
%% Define reconstruction system model parameter
% camera settings
pixel_size_native=3.45e-3;
pixel_size_recon=4*pixel_size_native;
num_pixels=63; % number of pixels in the reconstructed hyperspectral image
x=-floor(num_pixels/2):floor((num_pixels-1)/2);
y=x;
[X,Y]=meshgrid(x,y);
% fiber spectrometer acquisition mask
beam_waist=25e-3; % 50um fiber core size
gaussian_order=8;
sigma=beam_waist/pixel_size_recon;
boxing_wdith=2*sigma;

ref_image_ind=0;
num_images=1024; % number of images/spectrum acquired
num_wavelength=2027; % number of wavelength channels in each spectrum from usb2000zz.py
image_prefix='solar';
spectrum_prefix='spectrum';

%% Build system matrix
% (xoffSet, yoffSet) denote the plate-solved coordinates in pixels
% relative to the reference image
xoffset_save=zeros(num_images,1);
yoffset_save=zeros(num_images,1);
img_ref=imread(['./results/',image_prefix,'_',num2str(ref_image_ind),'.png']);
A=zeros(num_images,numel(X));
for ind_image=1:num_images
% xoffSet=0;
% yoffSet=0;
% Plate-solve each image with xcorr
I=imread(['./results/',image_prefix,'_',num2str(ind_image-1),'.png']);
GI=fftshift(fft2(ifftshift(double(I))));
Gref=fftshift(fft2(ifftshift(double(img_ref))));
R=GI.*conj(Gref);
img_corr=fftshift(ifft2(ifftshift(R)));
% img_corr=normxcorr2(img_ref,I);
[~,index_img_corr]=max(abs(img_corr(:)));
[ypeak,xpeak]=ind2sub(size(img_corr),index_img_corr);
yoffset = (ypeak-(floor((size(img_ref,1)-1)/2)+1))/(pixel_size_recon/pixel_size_native);
xoffset = (xpeak-(floor((size(img_ref,2)-1)/2)+1))/(pixel_size_recon/pixel_size_native);
% Fiber acquisition mask of each acquisition
fiber_mask=exp(-(sqrt((X-xoffset).^2+(Y-yoffset).^2)/sigma).^gaussian_order);
boxing_mask=abs(X-xoffset)<boxing_wdith & abs(Y-yoffset)<boxing_wdith;
fiber_mask=fiber_mask.*boxing_mask;
if sum(abs(fiber_mask(:)))>0
    fiber_mask=fiber_mask/sum(abs(fiber_mask(:)));
end

% figure(1);
% imagesc(x,y,fiber_mask);colorbar;
% axis image;colormap jet;

A(ind_image,:)=fiber_mask(:).';
xoffset_save(ind_image)=xoffset;
yoffset_save(ind_image)=yoffset;
end
A=sparse(A);

figure(2);
imagesc(A);colorbar;axis image;
title('System matrix A');
figure(3);
scatter(xoffset_save,yoffset_save,4,1:num_images,'filled');
axis square;colormap jet;
title('Sampling points');

%% Reconstruct hyperspectral image
% synthesize IFU image
ifu=zeros(num_images,num_wavelength);
for ind_image=1:num_images
ld=load(['./results/',spectrum_prefix,'_',num2str(ind_image),'.mat']);
ifu(ind_image,:)=ld.spectrum;
end
% image_recon_ifu=A\ifu;
image_recon_ifu=A.'*ifu;
image_recon=reshape(image_recon_ifu,length(y),length(x),[]);

ind_wavelength=775; % wavelength for display
figure(4);
imagesc(image_recon(:,:,ind_wavelength));colorbar;
axis image;colormap gray;